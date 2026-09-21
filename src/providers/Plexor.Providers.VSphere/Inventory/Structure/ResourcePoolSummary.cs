// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ResourcePoolSummary — vCenter /api/vcenter/resource-pool wire DTO.
// Init-property record per anti-patterns.md §2 (no positional records
// on wire shapes). Sealed per naming-and-types.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Inventory.Structure;

/// <summary>
///     Resource pool — vCenter placement target finer-grained than
///     a cluster. Optional in the v1 provisioning flow (cluster is
///     the default); surfaced in the inventory so a future iteration
///     can target pools directly.
/// </summary>
public sealed record ResourcePoolSummary
{
    /// <summary>Resource pool mo-ref.</summary>
    public required string Moref { get; init; }

    /// <summary>Resource pool name.</summary>
    public required string Name { get; init; }

    /// <summary>Parent cluster mo-ref.</summary>
    public required string ClusterMoref { get; init; }
}
