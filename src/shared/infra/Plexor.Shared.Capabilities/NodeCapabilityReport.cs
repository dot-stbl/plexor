// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeCapabilityReport — the wire shape the NodeAgent sends to the
// control plane after running all capability probes. A flat list of
// capability names + a probed-at timestamp. No structured record
// fields (no NestedVirt boolean, no K3sServer flag) — those are
// just more capabilities ("nested-virt", "k3s-server") with the
// same string vocabulary.
//
// This is intentionally minimal: the control plane's scheduler
// intersects the node's capability set with each service's
// validRuntimes set. Boolean flags would force a "what does this
// mean" lookup; a flat set is straightforward and extensible.
// ============================================================================

namespace Plexor.Shared.Capabilities;

/// <summary>
///     Result of running all registered <see cref="ICapabilityProbe" />
///     instances on a node. Sorted by capability name so the JSON
///     wire format is deterministic (tests assert exact order).
/// </summary>
/// <param name="Capabilities">
///     Union of every capability name every probe confirmed. Empty
///     if no probe contributed anything (rare — at least
///     <c>host-bridge</c> is always present).
/// </param>
/// <param name="ProbedAt">
///     UTC timestamp the aggregator finished. Lets the control
///     plane know the report's age — stale reports may need
///     re-probe.
/// </param>
public sealed record NodeCapabilityReport(
    IReadOnlyCollection<string> Capabilities,
    DateTimeOffset ProbedAt)
{
    /// <summary>
    ///     Pretty-print for logs / dashboard. "capabilities:
    ///     docker-runtime, host-bridge, nested-virt; probedAt: 2026-…".
    /// </summary>
    public override string ToString()
    {
        return $"capabilities={string.Join(",", Capabilities)}; " +
               $"probedAt={ProbedAt:O}";
    }
}