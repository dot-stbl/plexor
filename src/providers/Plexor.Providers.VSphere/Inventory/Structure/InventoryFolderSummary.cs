// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InventoryFolderSummary — vCenter /api/vcenter/folder wire DTO.
// Init-property record per anti-patterns.md §2 (no positional records
// on wire shapes). Sealed per naming-and-types.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Inventory.Structure;

/// <summary>
///     vCenter inventory folder — a logical grouping inside a
///     datacenter. Plexor maps each tenant Folder to one vCenter
///     inventory folder so resources land in the right group without
///     requiring the caller to know the underlying mo-ref.
/// </summary>
public sealed record InventoryFolderSummary
{
    /// <summary>Folder mo-ref.</summary>
    public required string Moref { get; init; }

    /// <summary>Folder name.</summary>
    public required string Name { get; init; }

    /// <summary>Folder kind — <c>"DATACENTER"</c>, <c>"DATA_CENTER"</c>,
    /// <c>"FOLDER"</c>, <c>"VM"</c>, <c>"HOST"</c>, <c>"STORAGE"</c>,
    /// <c>"NETWORK"</c>. Plexor uses <c>"VM"</c> for VM-folder
    /// targeting.</summary>
    public required string Kind { get; init; }

    /// <summary>Parent folder mo-ref, null when this is a root folder.</summary>
    public string? ParentMoref { get; init; }
}
