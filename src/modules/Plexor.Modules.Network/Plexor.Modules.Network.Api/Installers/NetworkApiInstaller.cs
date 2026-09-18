// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NetworkApiInstaller — DI registration extension for the Network API
// layer. Registers the FluentValidation validators that the endpoint
// handlers resolve via [FromServices]. Hosts compose it as
// builder.Services.AddNetworkApiCore() after AddNetworkApplicationCore
// + AddNetworkInfrastructureCore.
// ============================================================================

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Network.Api.Models.Requests;
using Plexor.Modules.Network.Api.Validation;

namespace Plexor.Modules.Network.Api.Installers;

/// <summary>
///     DI registration extension for the Network API layer.
/// </summary>
public static class NetworkApiInstaller
{
    /// <summary>
    ///     Register the Network API layer's services.
    /// </summary>
    public static IServiceCollection AddNetworkApiCore(this IServiceCollection services)
    {
        // FluentValidation — scoped validators resolved by the POST
        // endpoints via [FromServices]. Mirrors the Storage
        // BrandingApiInstaller pattern.
        services.AddScoped<IValidator<CreateFloatingIpRequest>, CreateFloatingIpRequestValidator>();
        services.AddScoped<IValidator<CreateLoadBalancerRequest>, CreateLoadBalancerRequestValidator>();

        return services;
    }
}
