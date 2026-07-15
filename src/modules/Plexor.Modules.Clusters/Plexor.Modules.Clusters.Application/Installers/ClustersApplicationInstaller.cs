// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClustersApplicationInstaller — DI registration for the Clusters
// Application layer. No-op for v0.1 (handlers live in Infrastructure);
// kept as the canonical registration surface for options + future
// Application-layer services (validators, notifications).
// ============================================================================

using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Modules.Clusters.Application.Installers;

/// <summary>
///     DI registration extension for the Clusters Application layer.
///     Called by the Host composition root before
///     <c>AddClustersInfrastructureCore</c>.
/// </summary>
public static class ClustersApplicationInstaller
{
    /// <summary>
    ///     Register Clusters Application services. v0.1 is a no-op
    ///     placeholder — handlers resolve from Infrastructure, and the
    ///     Application layer is pure DTOs + the <c>ICommandHandler</c>
    ///     contract.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddClustersApplicationCore(this IServiceCollection services)
    {
        return services;
    }
}
