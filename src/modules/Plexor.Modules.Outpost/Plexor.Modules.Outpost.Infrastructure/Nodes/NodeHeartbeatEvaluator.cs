// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeHeartbeatEvaluator — pure classifier for node health.
//
// Reads the node's last heartbeat + the configured staleness
// thresholds; returns one of three derived states (Healthy / Stale /
// Unhealthy). Implementation is pure (no I/O, no side effects) so
// unit tests can call Evaluate directly without standing up a DB.
//
// The evaluator also implements INodeHeartbeatEvaluator to keep the
// configuration-bindable threshold + the classification logic
// together — splitting them across two types would force every
// caller to wire both into DI for no benefit.
// ============================================================================

using Microsoft.Extensions.Options;
using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Application.Abstractions;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Outpost.Infrastructure.Nodes;

/// <summary>
///     Configuration-bound classifier. Bound from the
///     <c>Outpost:Heartbeat</c> section in TOML; defaults match the
///     v0.1 30-second heartbeat cadence + 90-second unhealthy window.
/// </summary>
public sealed class OutpostHeartbeatOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Outpost:Heartbeat";

    /// <summary>
    ///     Window after the last heartbeat during which the node is
    ///     considered Healthy. Default: 30s (one heartbeat cadence).
    /// </summary>
    public TimeSpan HealthyWindow { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Window after the last heartbeat after which the node is
    ///     considered Unhealthy. Between Healthy and Unhealthy the
    ///     node is Stale. Default: 90s (3 missed heartbeats).
    /// </summary>
    public TimeSpan UnhealthyWindow { get; init; } = TimeSpan.FromSeconds(90);
}

/// <summary>
///     INodeHeartbeatEvaluator implementation backed by
///     <see cref="OutpostHeartbeatOptions" />. Singleton — the
///     thresholds are read at startup; the classification itself is
///     allocation-free.
/// </summary>
/// <param name="options">Bound from <c>Outpost:Heartbeat</c>.</param>
public sealed class NodeHeartbeatEvaluator(IOptions<OutpostHeartbeatOptions> options)
    : INodeHeartbeatEvaluator
{
    private readonly OutpostHeartbeatOptions current = options.Value;

    /// <inheritdoc />
    public NodeHealth Evaluate(NodeId nodeId, DateTimeOffset? lastHeartbeatAt, DateTimeOffset now)
    {
        if (lastHeartbeatAt is null)
        {
            // Never heartbeated → unreachable.
            return NodeHealth.Unhealthy;
        }

        var age = now - lastHeartbeatAt.Value;
        if (age <= current.HealthyWindow)
        {
            return NodeHealth.Healthy;
        }

        if (age <= current.UnhealthyWindow)
        {
            return NodeHealth.Stale;
        }

        return NodeHealth.Unhealthy;
    }
}