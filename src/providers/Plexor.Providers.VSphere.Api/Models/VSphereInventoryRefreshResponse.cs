// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereInventoryRefreshResponse — wire shape for
// POST /api/v1/vsphere/inventory/refresh (202 Accepted body). Init-property
// class per anti-patterns.md §2 (no positional records on wire shapes).
// ============================================================================

namespace Plexor.Providers.VSphere.Api.Models;

/// <summary>
///     Body of the 202 Accepted response from a successful refresh.
///     Caller polls the GET inventory endpoint with the returned
///     <see cref="SnapshotId" /> once the cache has settled.
/// </summary>
public sealed class VSphereInventoryRefreshResponse
{
    /// <summary>Snapshot id (UUID v7) the refresh just produced.</summary>
    public required Guid SnapshotId { get; init; }

    /// <summary>Status — always <c>"SUCCESS"</c> on a 202 response.
    /// Failure responses surface as ProblemDetails instead.</summary>
    public string Status { get; init; } = "SUCCESS";
}
