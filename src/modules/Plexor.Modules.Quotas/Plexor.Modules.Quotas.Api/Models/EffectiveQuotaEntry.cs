// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EffectiveQuotaEntry — projection returned by GET /api/v1/quotas/effective.
// One row per catalog key, holding the resolved value + the origin
// scope that supplied it ("definition" / "assignment" / "org" / etc.).
// ============================================================================

namespace Plexor.Modules.Quotas.Api.Models;

/// <summary>
///     One resolved effective value, plus the scope kind whose
///     assignment (or catalog default) supplied it. Returned by
///     <c>GET /api/v1/quotas/effective</c>.
/// </summary>
/// <remarks>
///     <para><b>Origin serialisation.</b> The origin is the
///     <see cref="Origin" /> member of the scope walker:
///     <list type="bullet">
///       <item><c>"Folder"</c> — folder-scoped assignment won the walk.</item>
///       <item><c>"Team"</c> — team-scoped assignment (Phase 2).</item>
///       <item><c>"Org"</c> — org-scoped assignment won the walk.</item>
///       <item><c>"Definition"</c> — no assignment; the catalog's
///       built-in <c>DefaultValue</c> won.</item>
///       <item><c>"Unlimited"</c> — neither an assignment nor a
///       <c>DefaultValue</c> exists. <see cref="Value" /> is 0 in this
///       case.</item>
///     </list></para>
/// </remarks>
public sealed class EffectiveQuotaEntry
{
    /// <summary>Catalog key (e.g. <c>"compute.vms.count"</c>).</summary>
    public string DefinitionKey { get; init; } = string.Empty;

    /// <summary>Resolved limit value. <c>0</c> when
    /// <see cref="Origin" /> = <c>"Unlimited"</c>.</summary>
    public decimal Value { get; init; }

    /// <summary>Which scope (or default) supplied the value — see the
    /// <see cref="Origin" /> remarks above.</summary>
    public string Origin { get; init; } = string.Empty;
}
