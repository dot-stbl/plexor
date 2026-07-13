// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorSigilApiServiceCollectionExtensions — DI registration for the
// Sigil API layer. Hosts compose it as
//   builder.Services.AddPlexorSigilApi();
// after AddSigilApplicationCore + AddSigilInfrastructureCore.
// ============================================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Errors;
using Plexor.Modules.Sigil.Infrastructure.Users;

namespace Plexor.Modules.Sigil.Api;

/// <summary>
///     DI registration helpers for the Sigil API project. Wires
///     authentication command/query handlers (scoped per-request) and
///     the global <see cref="IdentityExceptionHandler" /> that maps
///     domain exceptions to RFC 7807 ProblemDetails responses.
/// </summary>
public static class PlexorSigilApiServiceCollectionExtensions
{
    /// <summary>
    ///     Register the Sigil API layer's services. Application-layer
    ///     interfaces (<see cref="Application.Auth.IPermissionResolver" />,
    ///     <see cref="Application.Auth.ITokenIssuer" />,
    ///     <see cref="Application.Auth.IPasswordHasher" />) are
    ///     expected to be registered upstream by
    ///     <c>AddSigilInfrastructureCore</c>.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddPlexorSigilApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Handlers — scoped per request so DbContext is reused across
        // the multi-DB-roundtrip pipeline (login does user lookup,
        // password verify, failed-login update, refresh issue,
        // permission resolve, access issue).
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<RefreshCommandHandler>();
        services.AddScoped<LogoutCommandHandler>();
        services.AddScoped<MeQueryHandler>();

        // User CRUD handlers — same scoped lifetime (DbContext reuse).
        services.AddScoped<CreateUserCommandHandler>();
        services.AddScoped<UpdateUserCommandHandler>();
        services.AddScoped<DisableUserCommandHandler>();
        services.AddScoped<GetUserQueryHandler>();
        services.AddScoped<ListUsersQueryHandler>();

        // User lookup — read-only, scoped.
        services.AddScoped<IUserLookup, EfUserLookup>();

        // Global exception handler. Order matters: this one runs
        // first; non-IdentityException passes through to the default
        // 500 handler.
        services.AddExceptionHandler<IdentityExceptionHandler>();

        return services;
    }
}
