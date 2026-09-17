// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Plexor.Modules.Outpost — Application layer.
//
// Outpost is the host-side node registry: tracks every Plexor.NodeAgent that
// has joined a cluster (hostname, IP, hardware, status, last heartbeat) in
// the `outpost` PostgreSQL schema (architecture theme).
//
// See AGENTS.md §"Naming: architecture theme vs C# concept" — schema = `outpost`,
// concept = NodeRecord (the C# class that names the row).
//
// Layering:
//   * Domain entities (NodeRecord, NodeStatus, NodeSpec, NodeHealth) live in
//     Application because they are flat data with no behaviour — a separate
//     Domain project would be overhead.
//   * Outpost.Application references Plexor.Modules.Clusters.Domain for
//     NodeRole (cluster-scoped concept owned by Clusters). Forward plan:
//     promote NodeRole to Plexor.Shared.Kernel when a 3rd module needs it.
// ============================================================================

namespace Plexor.Modules.Outpost.Application;

/// <summary>
///     Stable machine-readable codes for Outpost-raised errors. The
///     exception handler in Outpost.Infrastructure maps each code to a
///     ProblemDetails <c>code</c> extension; clients branch on the code,
///     not on the message.
/// </summary>
public static class OutpostExceptions
{
    /// <summary>The supplied join token is invalid, revoked, or expired.</summary>
    public const string InvalidJoinToken = "outpost.join_token.invalid";

    /// <summary>The cluster the join token belongs to is missing or offline.</summary>
    public const string ClusterNotFound = "outpost.cluster.not_found";

    /// <summary>A node with the supplied id does not exist (or is in a different cluster).</summary>
    public const string NodeNotFound = "outpost.node.not_found";

    /// <summary>Hostname is already taken in the cluster.</summary>
    public const string NodeHostnameTaken = "outpost.node.hostname_taken";

    /// <summary>Node role request does not match the join token's intended role.</summary>
    public const string NodeRoleMismatch = "outpost.node.role_mismatch";
}