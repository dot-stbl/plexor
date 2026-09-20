// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DatacenterSummary — vCenter /api/vcenter/datacenter wire DTO.
// Init-property record per anti-patterns.md §2 (no positional records
// on wire shapes). Sealed per naming-and-types.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Inventory.ReadModels;

/// <summary>
///     Top-level vCenter inventory container — a datacenter holds
///     clusters, which hold hosts, which hold VMs.
/// </summary>
public sealed record DatacenterSummary
{
    /// <summary>vCenter managed-object reference
    /// (e.g. <c>"datacenter-1"</c>).</summary>
    public required string Moref { get; init; }

    /// <summary>Human-readable datacenter name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional vCenter inventory folder path. Null when the
    /// datacenter lives at the root.</summary>
    public string? FolderPath { get; init; }
}
