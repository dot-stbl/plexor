// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BrandingApiInstaller — DI registration for the Branding API layer.
// Hosts compose it as `builder.Services.AddBrandingApiCore()` after
// AddBrandingApplicationCore + AddBrandingInfrastructureCore.
// ============================================================================

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Branding.Api.Models.Requests;
using Plexor.Modules.Branding.Api.Validation;

namespace Plexor.Modules.Branding.Api.Installers;

/// <summary>
///     DI registration for the Branding API layer. The
///     <see cref="Plexor.Modules.Branding.Api.Controllers.BrandingController" />
///     is discovered by <c>AddApplicationPart</c> in the host
///     (mirrors <c>QuotasController</c>); this installer wires the
///     request-body validators that the controller resolves via
///     <c>[FromServices]</c>.
/// </summary>
public static class BrandingApiInstaller
{
    /// <summary>
    ///     Register the Branding API layer's services.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddBrandingApiCore(this IServiceCollection services)
    {
        services.AddScoped<IValidator<UpsertGlobalThemeConfigRequest>, UpsertGlobalThemeConfigRequestValidator>();
        services.AddScoped<IValidator<UpsertOrgThemeConfigRequest>, UpsertOrgThemeConfigRequestValidator>();
        return services;
    }
}