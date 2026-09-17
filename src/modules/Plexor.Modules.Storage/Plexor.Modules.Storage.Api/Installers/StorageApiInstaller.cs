// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// StorageApiInstaller — DI registration extension for the Storage API
// layer. Registers the FluentValidation validators that the endpoint
// handlers resolve via [FromServices]. Hosts compose it as
// builder.Services.AddStorageApiCore() after AddStorageApplicationCore
// + AddStorageInfrastructureCore.
// ============================================================================

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Storage.Api.Models.Requests;
using Plexor.Modules.Storage.Api.Validation;

namespace Plexor.Modules.Storage.Api.Installers;

/// <summary>
///     DI registration extension for the Storage API layer.
/// </summary>
/// <remarks>
///     <para><b>Endpoints are mapped separately.</b>
///     <see cref="Plexor.Modules.Storage.Api.Endpoints.StorageEndpoints.MapStorageEndpoints" />
///     is invoked explicitly from the host
///     (<c>app.MapStorageEndpoints()</c>) — the wiring sits
///     next to <c>app.MapControllers()</c> in <c>Program.cs</c>, not
///     in the DI installer.</para>
/// </remarks>
public static class StorageApiInstaller
{
    /// <summary>
    ///     Register the Storage API layer's services.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddStorageApiCore(this IServiceCollection services)
    {
        // FluentValidation — scoped validators resolved by the POST
        // endpoints via [FromServices]. Mirrors the Branding
        // pattern (BrandingApiInstaller.AddBrandingApiCore).
        services.AddScoped<IValidator<CreateVolumeRequest>, CreateVolumeRequestValidator>();
        services.AddScoped<IValidator<CreateBucketRequest>, CreateBucketRequestValidator>();

        return services;
    }
}
