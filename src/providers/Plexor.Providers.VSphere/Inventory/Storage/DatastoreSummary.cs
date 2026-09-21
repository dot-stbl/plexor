// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DatastoreSummary — vCenter /api/vcenter/datastore wire DTO.
// Init-property record per anti-patterns.md §2 (no positional records
// on wire shapes). Sealed per naming-and-types.md §2.
// ============================================================================

namespace Plexor.Providers.VSphere.Inventory.Storage;

/// <summary>
///     Datastore summary — backing storage for VMs. Surfaced for
///     inventory completeness; not consumed by the v1 provisioning
///     flow (vCenter picks the datastore from the cluster default
///     unless the clone request specifies one).
/// </summary>
public sealed record DatastoreSummary
{
    /// <summary>Datastore mo-ref.</summary>
    public required string Moref { get; init; }

    /// <summary>Datastore name.</summary>
    public required string Name { get; init; }

    /// <summary>Type — <c>"VMFS"</c>, <c>"NFS"</c>, <c>"VSAN"</c>,
    /// <c>"VVOL"</c>, etc.</summary>
    public required string Type { get; init; }

    /// <summary>Total capacity in MiB.</summary>
    public long CapacityMib { get; init; }

    /// <summary>Free space in MiB.</summary>
    public long FreeMib { get; init; }
}
