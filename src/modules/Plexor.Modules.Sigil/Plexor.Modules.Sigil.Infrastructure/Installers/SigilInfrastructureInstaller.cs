// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SigilInfrastructureInstaller — single registration entry for the
// Sigil (Identity) Infrastructure layer. Hosts compose it as
//   builder.Services.AddSigilInfrastructureCore(builder.Configuration);
// Wires ICurrentUser + IPasswordHasher + IRefreshTokenStore +
// ISigningKeyRepository + IJwtSigningService + the signing-key
// bootstrapper hosted service.
// ============================================================================

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.Auth;
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
    /// <param name="configuration">
    ///     Reserved for Options binding in 3.2+. Pre-bound so adding
    ///     <c>IOptions</c> later doesn't change the call site.
    /// </param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddSigilInfrastructureCore(
        this IServiceCollection services,
        IConfiguration configuration)
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
                _ => { });
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

        return services;
    }
}
