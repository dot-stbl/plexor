// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeAgentState — mutable per-process state shared between the join
// step and the heartbeat + poll loops. Held as a singleton; the
// orchestrator (NodeAgentWorker) writes <see cref="Current" /> on
// successful join, the loops read it (under lock-free volatile).
// Extracted from NodeAgentWorker.cs (Sep 2026) per §9 (no private
// fields with cross-method writes between helper methods).
// ============================================================================

using Plexor.NodeAgent.Composition;

namespace Plexor.NodeAgent;

/// <summary>
///     Singleton container for the join-derived
///     <see cref="NodeIdentity" /> + the post-join rendezvous URL.
///     All access goes through the volatile <see cref="Current" />
///     property — the join phase writes it; the heartbeat + poll
///     loops read it.
/// </summary>
public sealed class NodeAgentState
{
    /// <summary>Currently-joined identity; null until the first
    /// successful POST /nodes/register.</summary>
    public NodeIdentity? Current { get; set; }
}