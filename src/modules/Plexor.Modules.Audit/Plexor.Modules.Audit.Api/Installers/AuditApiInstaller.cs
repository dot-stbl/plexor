// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditApiInstaller — single registration entry for the Audit API layer.
// Hosts compose it as `builder.Services.AddAuditApiCore()` after
// AddAuditApplicationCore + AddAuditInfrastructureCore.
//
// 5.2 ships no per-request Api-layer DI surface (the read endpoint
// resolves AuditDbContext from the composition root's per-module
// registration and ICurrentUser from Sigil). The installer is the
// wiring point for future request-body validators + 5.3 retention
// options — keeping the slot occupied now so Program.cs's call chain
// stays stable as the layer grows.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Modules.Audit.Api.Installers;

/// <summary>
///     DI registration for the Audit API layer. v1 (5.2) has no
///     services to register — the read endpoint resolves its
///     dependencies (<c>AuditDbContext</c>, <c>ICurrentUser</c>)
///     through the composition root's existing registrations.
/// </summary>
/// <remarks>
///     <para><b>Mirrors <c>QuotasApiInstaller</c> at the 4.5.b
///     cut-line.</b> The Quotas API installer was empty until 4.5.g.3
///     shipped the PUT validator; the Audit installer follows the
///     same shape so future Phase 5.x additions (retention options,
///     admin UI helpers) drop in without restructuring the
///     Program.cs chain.</para>
/// </remarks>
public static class AuditApiInstaller
{
    /// <summary>
    ///     Register the Audit API layer's services. AuditDbContext +
    ///     ICurrentUser are registered upstream by the composition
    ///     root + <c>AddAuditInfrastructureCore</c> /
    ///     <c>AddSigilApplicationCore</c>; this installer exists for
    ///     the wiring-point contract, not for current registrations.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddAuditApiCore(this IServiceCollection services)
    {
        return services;
    }
}
