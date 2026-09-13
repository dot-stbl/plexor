// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaDefinitionSummary — projection of QuotaDefinition returned by
// GET /api/v1/quotas/definitions. Surfaces the catalog to operators.
// `init`-only properties for object-initializer compatibility (matches
// the Sigil UserSummary shape used elsewhere in the API surface).
// ============================================================================

namespace Plexor.Modules.Quotas.Api.Models;

/// <summary>
///     One catalog entry, returned as part of
///     <c>GET /api/v1/quotas/definitions</c>. Surfaces the limit key,
///     unit, period, and built-in default to operators without exposing
///     the EF entity itself.
/// </summary>
/// <remarks>
///     <para><b>Why serialise the unit / period as strings.</b>
///     <see cref="Plexor.Modules.Quotas.Domain.QuotaUnit" /> and
///     <see cref="Plexor.Modules.Quotas.Domain.QuotaPeriod" /> are stored
///     as <c>varchar(16)</c> in PostgreSQL; serialising them as
///     <see cref="string" /> via <c>ToString()</c> matches the wire shape
///     the catalog seeder writes and the dashboard reads.</para>
/// </remarks>
public sealed class QuotaDefinitionSummary
{
    /// <summary>Catalog row id (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Stable catalog identifier (<c>"compute.vms.count"</c>).</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Human-readable label, or <see langword="null" /> when the
    /// catalog row predates the description field.</summary>
    public string? Description { get; init; }

    /// <summary>Unit of measurement, serialised as the enum member name
    /// (<c>"Count"</c>, <c>"Gb"</c>, <c>"Vcpu"</c>, <c>"ReqPerHour"</c>).</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>Time window the value is measured over, serialised as
    /// the enum member name (<c>"None"</c>, <c>"Hour"</c>, ...).</summary>
    public string Period { get; init; } = string.Empty;

    /// <summary>Built-in default value when no assignment exists for
    /// the requested scope. <see langword="null" /> means unlimited.</summary>
    public decimal? DefaultValue { get; init; }
}
