// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeRecordResponseMapper — NodeRecord → NodeResponse projection for
// the GET /api/v1/nodes endpoints. Internal static helper because §1a
// forbids private methods on production classes; the controller file
// (NodesController.cs) holds the routes, this file holds the projection.
// ============================================================================

using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Api.Models;
using Plexor.Shared.NodeApi;

namespace Plexor.Modules.Outpost.Api.Controllers;

/// <summary>
///     Internal projection helpers for the nodes controller. File-level
///     helpers would be invisible to NodesController; internal static
///     keeps the helper off the public API surface.
/// </summary>
internal static class NodeRecordResponseMapper
{
    /// <summary>
    ///     Project a domain <see cref="NodeRecord" /> onto the public
    ///     <see cref="NodeResponse" /> wire shape.
    /// </summary>
    /// <param name="node">Source row.</param>
    public static NodeResponse ToResponse(NodeRecord node)
    {
        return new NodeResponse(
            node.Id,
            node.ClusterId,
            node.OrgId,
            node.Hostname,
            node.IpAddress,
            node.Role,
            node.Status,
            new NodeHardwareSpec(
                node.Spec.Vcpu,
                node.Spec.RamGb,
                node.Spec.DiskGb,
                node.Spec.Providers),
            node.IsoVersion,
            node.LastHeartbeatAt,
            node.CreatedAt,
            node.UpdatedAt);
    }
}