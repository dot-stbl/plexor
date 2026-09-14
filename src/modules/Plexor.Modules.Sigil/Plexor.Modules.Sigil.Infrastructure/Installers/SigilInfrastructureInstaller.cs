// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SigilInfrastructureInstaller — single registration entry for the
// Sigil (Identity) Infrastructure layer. Hosts compose it as
//   builder.Services.AddSigilInfrastructureCore(builder.Configuration);
// Wires ICurrentUser + IPasswordHasher + IRefreshTokenStore +
// ISigningKeyRepository + IJwtSigningService + the signing-key
// bootstrapper hosted service.
// ============================================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.AuthProviders;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders;
using Plexor.Modules.Sigil.Infrastructure.CurrentUser;
using Plexor.Shared.Authorization;

namespace Plexor.Modules.Sigil.Infrastructure.Installers;

/// <summary>
///     Registers Infrastructure-layer services for the Sigil module.
///     </summary>
/// <remarks>
///     <para><b>Why one installer.</b> The DI convention in
///     <c>di-installer.md</c> says one <c>Add&lt;Module&gt;Core</c> per
///     module. Hosts compose an explicit chain — no reflection.</para>
///     <para><b>Why Infrastructure wires <see cref="ICurrentUser" />.</b>
///     <see cref="HttpContextCurrentUser" /> is an Infrastructure
///     concern (depends on <see cref="Microsoft.AspNetCore.Http" />).
///     Application layer defines the interface; Infrastructure binds
///     the impl. Both layers' installers live next to each other so
///     the call site is one chain: <c>AddSigilApplicationCore().AddSigilInfrastructureCore()</c>.</para>
///     <para><b>Why <c>services.AddXxx&lt;&gt;()</c> without
///     <c>_ = </c>.</b> <c>IServiceCollection.Add*</c> returns the
///     collection for fluent chaining; this installer doesn't chain,
///     so the return value is unused. <c>_ = services.AddXxx()</c>
///     is the meaningless discard pattern banned in
///     <c>async-and-tasks.md</c> §6 (generalized to "discarded
///     chained returns") — just call without the discard prefix.</para>
/// </remarks>
public static class SigilInfrastructureInstaller
{
    /// <summary>Register Sigil Infrastructure-layer services.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddSigilInfrastructureCore(
        this IServiceCollection services)
    {
        // ICurrentUser implementation lives in Infrastructure.
        // Lifetime is Scoped because IHttpContextAccessor.HttpContext
        // is per-request; the singleton accessor itself is fine to
        // share across scopes.
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        // Built-in PBKDF2 password hasher. Singleton — the underlying
        // Rfc2898DeriveBytes is thread-safe. Wrapped by PlexorPasswordHasher
        // (3.2.b) which exposes the Application-layer interface.
        services.AddSingleton<PasswordHasher<User>>();
        services.AddSingleton<IPasswordHasher, PlexorPasswordHasher>();

        // Refresh-token storage. Scoped — DbContext is scoped; the
        // store holds no per-instance state beyond the constructor
        // dependency. Rotation runs inside an explicit transaction
        // (see EfRefreshTokenStore.RotateAsync).
        services.AddScoped<IRefreshTokenStore, EfRefreshTokenStore>();

        // Signing key repository. Scoped — DbContext is scoped.
        // JwtSigningService reads public keys; SigningKeyBootstrapper
        // writes the first keypair on startup.
        services.AddScoped<ISigningKeyRepository, EfSigningKeyRepository>();

        // JWT signing service. Singleton — holds no per-instance
        // state; every Issue / Verify call reads from the key
        // repository.
        services.AddSingleton<IJwtSigningService, JwtSigningService>();

        // Signing-key bootstrapper. Runs on startup; ensures at
        // least one active signing key exists before the first
        // HTTP request. "First writer wins" — multiple hosts
        // racing on the same empty table are reconciled by the
        // unique kid constraint + a retry-on-conflict path.
        services.AddHostedService<SigningKeyBootstrapper>();

        // Bearer auth scheme (Phase 3.6). The handler delegates
        // verification to IJwtSigningService — no separate
        // TokenValidationParameters pipeline. AddAuthentication
        // sets the default scheme; AddAuthorization makes
        // [Authorize] work without an explicit policy argument.
        services
            .AddAuthentication(BearerOptions.SchemeName)
            .AddScheme<BearerOptions, BearerAuthenticationHandler>(
                BearerOptions.SchemeName,
                static _ => { });
        services.AddAuthorization();

        // Phase 3.7 — permission policy provider + handler so that
        // [RequirePermission("vms.read")] on a controller resolves to
        // an Authorization policy that checks the caller's `permission`
        // claims at request time. Registered in the Sigil
        // Infrastructure installer because the handler depends on
        // ILogger which lives in the framework, and the handler is
        // application-scoped (per-request), but the project ref to
        // Plexor.Shared.Authorization is the only consumer-side
        // coupling — controllers in any module can use the attribute.
        services.AddPlexorAuthorization();

        // Phase 4 — permission resolver + token issuer. Resolver
        // walks role_bindings → roles on every token issue (login +
        // refresh) and bakes the union of permissions into the
        // access token's claims. TokenIssuer composes the resolver
        // and the JWT signing service so callers don't have to.
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddSingleton<ITokenIssuer, TokenIssuer>();

        // Phase 4 — role resolver. Same shape as IPermissionResolver
        // but projects to role.Name (used by the `role` claims baked
        // into the access token). Splitting permissions from roles
        // avoids a join + select-many on the same table.
        services.AddScoped<IRoleResolver, EfRoleResolver>();

        // Phase 4.6.2a — IAuthProvider implementation for the local
        // email+password backend. The bearer handler today bypasses
        // this provider; the future dispatcher (4.6.2c) routes the
        // bearer credential through it. Scoped — it depends on a
        // scoped DbContext (Realm) plus the per-request IUserLookup.
        services.AddScoped<IAuthProvider, SigilAuthProvider>();

        // Phase 4.6.2b — external OIDC backend. Registered as a
        // concrete type only; the existing `IAuthProvider` binding
        // (SigilAuthProvider above) stays as-is to avoid ASP.NET's
        // last-wins semantics on multiple IAuthProvider bindings.
        // The future dispatcher (4.6.2c) will resolve both
        // SigilAuthProvider and ExternalOidcAuthProvider
        // explicitly (via a typed factory or an
        // `IEnumerable<IAuthProvider>` variant that the Sigil
        // installer will register when 4.6.2c lands). For now,
        // `ExternalOidcAuthProvider` is reachable only via direct
        // injection — the bearer handler doesn't yet consult it.
        services.AddScoped<ExternalOidcAuthProvider>();

        // Phase 4.6.2b — JWKS fetch + 1h in-memory cache. Singleton
        // because the fetcher holds no per-request state beyond the
        // shared IMemoryCache. The named "Plexor-OidcDiscovery"
        // HttpClient is registered in Plexor.Host/Program.cs
        // (10s timeout, no auth, no retries).
        services.AddMemoryCache();
        services.AddSingleton<IJwksFetcher, JwksFetcher>();

        // Revocation checker — JwtSigningService calls it after
        // signature + lifetime validation succeeds so a stolen,
        // signature-valid JWT is rejected once the user is disabled
        // or rotates their password. Scoped because the EF lookup is.
        services.AddScoped<IUserRevocationChecker, EfUserRevocationChecker>();

        // API key auth — BearerAuthenticationHandler routes
        // kid_xxx.<secret> tokens here. Scoped (DbContext reuse).
        services.AddScoped<IApiKeyAuthenticationService, EfApiKeyAuthenticationService>();

        return services;
    }
}
