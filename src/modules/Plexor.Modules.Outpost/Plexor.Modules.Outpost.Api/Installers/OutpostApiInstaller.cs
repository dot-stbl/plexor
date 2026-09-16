// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OutpostApiInstaller — DI registration for the Outpost API layer.
// v0.1 ships no API-layer services (controllers are discovered via
// AddApplicationPart in Plexor.Host.Program.cs); the installer is the
// canonical surface for future API-layer DI (FluentValidation, OpenAPI
// transformers).
// ============================================================================

using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Modules.Outpost.Api.Installers;

/// <summary>
///     DI registration extension for the Outpost API layer. v0.1 is a
///     no-op placeholder; controllers are discovered via
///     <c>AddApplicationPart</c> in Plexor.Host's composition root.
/// </summary>
public static class OutpostApiInstaller
{
    /// <summary>
    ///     Register the Outpost API layer's services. Controllers
    ///     resolve their dependencies through the existing
    ///     registrations (OutpostDbContext, INodeRegistry, etc.).
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddOutpostApiCore(this IServiceCollection services)
    {
        return services;
    }
}