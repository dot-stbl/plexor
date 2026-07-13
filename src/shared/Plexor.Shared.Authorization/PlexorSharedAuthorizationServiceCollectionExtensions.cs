// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorSharedAuthorizationServiceCollectionExtensions — single
// registration entry for the Shared.Authorization module. Hosts compose
// it as
//   builder.Services.AddPlexorAuthorization();
// after AddAuthentication / AddAuthorization.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Shared.Authorization;

/// <summary>
///     DI registration helpers for the Shared.Authorization module.
/// </summary>
public static class PlexorSharedAuthorizationServiceCollectionExtensions
{
    /// <summary>
    ///     Register the permission policy provider and the matching
    ///     authorization handler. Both are required for
    ///     <c>[RequirePermission]</c> to resolve into running
    ///     authorization behaviour.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddPlexorAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }
}
