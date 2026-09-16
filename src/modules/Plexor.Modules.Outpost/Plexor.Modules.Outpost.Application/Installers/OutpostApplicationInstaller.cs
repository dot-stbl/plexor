// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OutpostApplicationInstaller — DI registration for the Outpost
// Application layer. v0.1 ships no Application-layer services
// (handlers live in Infrastructure); the installer is the canonical
// surface for future Application-layer DI (validators, options).
// ============================================================================

using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Modules.Outpost.Application.Installers;

/// <summary>
///     DI registration extension for the Outpost Application layer.
///     Called by the Host composition root before
///     <c>AddOutpostInfrastructureCore</c>.
/// </summary>
public static class OutpostApplicationInstaller
{
    /// <summary>
    ///     Register Outpost Application-layer services. v0.1 is a
    ///     no-op placeholder — handlers resolve from Infrastructure,
    ///     and the Application layer is pure records + abstractions.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddOutpostApplicationCore(this IServiceCollection services)
    {
        return services;
    }
}