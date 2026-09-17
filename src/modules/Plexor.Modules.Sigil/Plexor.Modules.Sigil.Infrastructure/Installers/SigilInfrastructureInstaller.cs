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
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Flows;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Provisioners;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Resolvers;
using Plexor.Modules.Sigil.Infrastructure.CurrentUser;
using Plexor.Shared.Authorization;
using Plexor.Shared.Kernel.AuthProviders;

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
        // email+password backend. Routed-to by the dispatcher (4.6.2c)
        // for `iss == "plexor"` tokens. Scoped — it depends on the
        // per-request IUserLookup + the scoped
        // IOrgAuthProviderConfigReader (registered by
        // AddRealmAuthProviders in the host composition root).
        // NOTE: kept as the `IAuthProvider` binding so callers that
        // took a dependency on `IAuthProvider.CanAuthenticateForAsync`
        // continue to work — `AuthProviderResolver` itself receives
        // both concrete providers via primary ctor (see below).
        services.AddScoped<IAuthProvider, SigilAuthProvider>();

        // Phase 4.6.2b — external OIDC backend. Registered as a
        // concrete type only (NOT as `IAuthProvider`) to avoid ASP.NET's
        // last-wins semantics on multiple `IAuthProvider` bindings.
        // The resolver (4.6.2c) consumes both SigilAuthProvider and
        // ExternalOidcAuthProvider via primary ctor — no
        // `IEnumerable<IAuthProvider>` enumeration needed for v1.
        services.AddScoped<ExternalOidcAuthProvider>();

        // Phase 4.6.2c — the auth provider resolver. Routes a raw
        // bearer credential to the right IAuthProvider based on the
        // JWT `iss` claim; caches the (iss → provider) map for 5
        // minutes. Scoped (mirrors the providers it dispatches to);
        // the IMemoryCache itself is the process-wide singleton
        // registered a few lines below.
        services.AddScoped<IAuthProviderResolver, AuthProviderResolver>();

        // Phase 4.6.2b — JWKS fetch + 1h in-memory cache. Singleton
        // because the fetcher holds no per-request state beyond the
        // shared IMemoryCache. The named "Plexor-OidcDiscovery"
        // HttpClient is registered in Plexor.Host/Program.cs
        // (10s timeout, no auth, no retries).
        services.AddMemoryCache();
        services.AddSingleton<IJwksFetcher, JwksFetcher>();

        // Phase 4.6.3a — PKCE generator (RFC 7636). Singleton;
        // depends only on the OS CSPRNG, no per-request state.
        services.AddSingleton<IPkceGenerator, PkceGenerator>();

        // Phase 4.6.3a — outbound token-exchange client for the OIDC
        // authorization-code flow (RFC 6749 §4.1.3). Singleton — the
        // IHttpClientFactory, IOrgAuthProviderConfigReader, and
        // OrgAuthProviderSecretProtector dependencies are all
        // singleton-or-shared; the client holds no per-request state.
        services.AddSingleton<IOidcTokenClient, OidcTokenClient>();

        // Phase 4.6.3b — server-side state for the OIDC
        // authorization-code + PKCE flow. Singleton — the underlying
        // IMemoryCache is process-wide and the OidcFlowStateStore
        // holds no per-request state. The PKCE verifier lives here
        // until the callback leg consumes it (10-minute TTL,
        // one-shot read+remove).
        services.AddSingleton<IOidcFlowStateStore, OidcFlowStateStore>();

        // Phase 4.6.3c — id_token validator for the OIDC callback
        // leg. Scoped — same shape as ExternalOidcAuthProvider (the
        // validator and the bearer-side provider share the
        // ExternalOidcAuthProviderHelpers primitives).
        services.AddScoped<IOidcIdTokenValidator, EfOidcIdTokenValidator>();

        // Phase 4.6.3c — find-or-create the Plexor User row + a
        // default viewer-role binding for a freshly-onboarded OIDC
        // user. Scoped — IdentityDbContext is per-request.
        services.AddScoped<IOidcUserProvisioner, EfOidcUserProvisioner>();

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
