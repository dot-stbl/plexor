// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotasApiInstaller — single registration entry for the Quotas API
// layer. Hosts compose it as
//   builder.Services.AddQuotasApiCore()
// after AddQuotasApplicationCore + AddQuotasInfrastructureCore.
// ============================================================================

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Quotas.Api.Models;
using Plexor.Modules.Quotas.Api.Validation;

namespace Plexor.Modules.Quotas.Api.Installers;

/// <summary>
///     DI registration for the Quotas API layer. The controllers are
///     discovered by <c>AddApplicationPart</c> in the host
///     (<c>Plexor.Host/Program.cs</c>); this installer only wires the
///     request-body validators that the controllers resolve via
///     <c>[FromServices]</c> on the action signature.
/// </summary>
/// <remarks>
///     <para><b>Why a new installer.</b> Before 4.5.g.3 the Quotas
///     API project had no DI surface — the controllers were wired via
///     <c>AddApplicationPart</c> and took all dependencies through
///     primary constructors. The PUT endpoint (4.5.g.3) is the first
///     endpoint that needs a FluentValidation validator, and the
///     validator lives in the same assembly as the request DTO (no
///     Application → Api reference cycle). This installer mirrors
///     <c>PlexorSigilApiServiceCollectionExtensions.AddPlexorSigilApi</c>
///     — same pattern, smaller scope.</para>
/// </remarks>
public static class QuotasApiInstaller
{
    /// <summary>
    ///     Register the Quotas API layer's services. Application-layer
    ///     interfaces (<c>IQuotaCatalog</c>, <c>IQuotaAssignmentRepository</c>,
    ///     <c>IQuotaScopeResolver</c>, <c>IQuotaUsageReader</c>,
    ///     <c>ICurrentUser</c>) are registered upstream by
    ///     <c>AddQuotasApplicationCore</c> + <c>AddQuotasInfrastructureCore</c>.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddQuotasApiCore(this IServiceCollection services)
    {

        // FluentValidation — scoped per request. The 4.5.g.3 PUT
        // endpoint resolves IValidator<UpsertQuotaAssignmentRequest>
        // via [FromServices] so the validator shares the request's
        // scope. No shared singleton state today; revisit if a future
        // validator caches catalog metadata.
        services.AddScoped<IValidator<UpsertQuotaAssignmentRequest>, UpsertQuotaAssignmentRequestValidator>();

        return services;
    }
}
