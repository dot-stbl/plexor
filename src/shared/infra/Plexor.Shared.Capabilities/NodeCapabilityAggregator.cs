// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeCapabilityAggregator — runs every registered ICapabilityProbe
// in parallel, unions their results, returns a single
// NodeCapabilityReport. One per process (singleton); injected with
// IEnumerable<ICapabilityProbe> so DI does the discovery.
//
// Failure mode: a probe that throws doesn't kill the report. We
// log a warning and continue. The aggregator's job is to give the
// control plane the best partial picture; an unprovable capability
// is worse than a missing capability entry.
// ============================================================================

using Microsoft.Extensions.Logging;

namespace Plexor.Shared.Capabilities;

/// <summary>
///     Orchestrates the per-provider <see cref="ICapabilityProbe" />
///     instances. Used by the NodeAgent at startup and on a periodic
///     timer; the result goes to <c>node.yaml</c> and to the
///     control plane via <c>POST /api/v1/nodes/{id}/capabilities</c>.
/// </summary>
public sealed class NodeCapabilityAggregator(
    IEnumerable<ICapabilityProbe> probes,
    ILogger<NodeCapabilityAggregator> logger)
{
    /// <summary>
    ///     Run every probe, collect capabilities, return a sorted
    ///     report. A throwing probe is logged and skipped — the
    ///     control plane's scheduler treats absent capabilities
    ///     as "node doesn't have it", which is the right behaviour
    ///     when the probe can't see through (e.g. permissions).
    /// </summary>
    public async Task<NodeCapabilityReport> AggregateAsync(
        CancellationToken cancellationToken = default)
    {
        var union = new SortedSet<string>(StringComparer.Ordinal);
        var probeList = probes.ToList();
        var tasks = new List<Task>(probeList.Count);

        foreach (var probe in probeList)
        {
            tasks.Add(RunOneAsync(probe, union, cancellationToken));
        }

        await Task.WhenAll(tasks);
        return new NodeCapabilityReport(union, DateTimeOffset.UtcNow);
    }

    private async Task RunOneAsync(
        ICapabilityProbe probe,
        SortedSet<string> union,
        CancellationToken cancellationToken)
    {
        try
        {
            var capabilities = await probe.ProbeAsync(cancellationToken);
            lock (union)
            {
                foreach (var capability in capabilities)
                {
                    union.Add(capability);
                }
            }
        }
        catch (Exception ex)
        {
            // Don't let one broken probe kill the whole report.
            // The control plane's scheduler treats absent
            // capabilities as "node doesn't have it" — exactly
            // what we want for a probe that failed.
            logger.LogWarning(
                ex,
                "Capability probe {Provider} failed; skipping its contribution",
                probe.ProviderName);
        }
    }
}
