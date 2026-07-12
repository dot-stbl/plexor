// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SigilInfrastructureInstaller — single registration entry for the
// Sigil (Identity) Infrastructure layer. Hosts compose it as
//   builder.Services.AddSigilInfrastructureCore(builder.Configuration);
// Currently wires ICurrentUser only; 3.2.b adds IPasswordHasher,
// 3.2.c adds IRefreshTokenStore, 3.3.a adds IJwtSigningService.
// ============================================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.CurrentUser;

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
///     <para><b>Future additions.</b>
///     <list type="bullet">
///       <item>3.2.b — <c>AddSingleton&lt;IPasswordHasher, PlexorPasswordHasher&gt;()</c></item>
///       <item>3.2.c — <c>AddScoped&lt;IRefreshTokenStore, EfRefreshTokenStore&gt;()</c></item>
///       <item>3.3.a — <c>AddSingleton&lt;IJwtSigningService, JwtSigningService&gt;()</c></item>
///       <item>3.3.b — <c>AddScoped&lt;ISigningKeyRepository, EfSigningKeyRepository&gt;()</c></item>
///       <item>3.4.a — <c>AddHostedService&lt;SigningKeyBootstrapper&gt;()</c></item>
///     </list>
///     None of these need their own <c>Add*</c> extension — they
///     land inside this single method.</para>
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
        _ = configuration;

        // ICurrentUser implementation lives in Infrastructure.
        // Lifetime is Scoped because IHttpContextAccessor.HttpContext
        // is per-request; the singleton accessor itself is fine to
        // share across scopes.
        _ = services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        // Built-in PBKDF2 password hasher. Singleton — the underlying
        // Rfc2898DeriveBytes is thread-safe. Wrapped by PlexorPasswordHasher
        // (3.2.b) which exposes the Application-layer interface.
        _ = services.AddSingleton<PasswordHasher<User>>();
        _ = services.AddSingleton<IPasswordHasher, PlexorPasswordHasher>();

        return services;
    }
}
