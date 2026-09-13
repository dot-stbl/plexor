// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaUsageEntry — projection returned by GET /api/v1/quotas/usage.
// Pairs the current consumption with the effective limit so the
// dashboard can render "X of Y used" / "approaching limit" badges
// without joining server-side or making the FE chase two endpoints.
// ============================================================================

namespace Plexor.Modules.Quotas.Api.Models;

/// <summary>
///     One usage snapshot row, paired with the resolved effective limit.
///     Returned by <c>GET /api/v1/quotas/usage</c>.
/// </summary>
/// <remarks>
///     <para><b>Threshold semantics.</b> <see cref="ThresholdPct" />
///     is <see langword="null" /> when current usage is below 80% of
///     the limit — the dashboard renders no badge in that case. When
///     usage is at or above 80%, the value is current / limit × 100,
///     rounded to one decimal place, so the badge can show the exact
///     percentage.</para>
/// </remarks>
public sealed class QuotaUsageEntry
{
    /// <summary>Catalog key (e.g. <c>"compute.vms.count"</c>).</summary>
    public string DefinitionKey { get; init; } = string.Empty;

    /// <summary>Current consumption value at this scope.</summary>
    public decimal Used { get; init; }

    /// <summary>Resolved effective limit (scope walker: folder → team →
    /// org → default). <see langword="null" /> = unlimited — the
    /// resolver returned null because no assignment and no
    /// <c>DefaultValue</c> exist.</summary>
    public decimal? EffectiveLimit { get; init; }

    /// <summary>Usage percentage rounded to one decimal place, or
    /// <see langword="null" /> when usage is below the 80% warning
    /// threshold. The dashboard renders a badge only when this is
    /// non-null.</summary>
    public decimal? ThresholdPct { get; init; }
}
