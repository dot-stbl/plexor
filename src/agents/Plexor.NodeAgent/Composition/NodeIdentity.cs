// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeIdentity — mutable per-node state held in the worker. Extracted
// from NodeAgentWorker.cs (Sep 2026) so the heartbeat + poll loops can
// share it via DI rather than via private fields on the worker (which
// §1a prohibits).
// ============================================================================

namespace Plexor.NodeAgent.Composition;

///<summary>
///    Per-node mutable state: identity + cursor.
/// </summary>
///<param name="NodeId">Wire-format node id (node_&lt;UUIDv7&gt;).</param>
///<param name="ClusterId">Wire-format cluster id (cluster_&lt;UUIDv7&gt;).</param>
///<param name="ClusterEndpoint">Post-join rendezvous point.</param>
///<param name="Cursor">Long-poll cursor for the command poll loop.</param>
public sealed record NodeIdentity(
    string NodeId,
    string ClusterId,
    string ClusterEndpoint,
    long Cursor);