// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeJoiner — owns the Plexor.NodeAgent registration call. Builds the
// wire RegisterNodeRequest (Hostname, IpAddress, Role, Hardware,
// IsoVersion, WireguardPublicKey), calls transport.JoinAsync, persists
// the mTLS cert directory, and writes the new identity into the
// shared NodeAgentState.
//
// Extracted from NodeAgentWorker.JoinOnceAsync (Sep 2026) per §9 (no
// private orchestration methods on BackgroundService classes).
// ============================================================================

using Plexor.NodeAgent.Abstractions;
using Plexor.NodeAgent.Composition;
using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent;

/// <summary>
///     Coordinates a single registration call. Constructor params carry
///     everything the joiner needs; the worker's mutable state goes
///     through the injected <see cref="NodeAgentState" />.
/// </summary>
/// <param name="transport">Refit-backed HTTP transport.</param>
/// <param name="nodeOptions">mTLS + directory options.</param>
/// <param name="state">Shared state; <see cref="NodeAgentState.Current" />
///     is set to the new identity on success.</param>
/// <param name="config">Node config (hostname, hardware, control-plane URL).</param>
/// <param name="logger">Structured logger.</param>
internal sealed class NodeJoiner(
    ICommandTransport transport,
    NodeAgentOptions nodeOptions,
    NodeAgentState state,
    NodeConfig config,
    ILogger<NodeJoiner> logger)
{
    /// <summary>
    ///     v0.1 placeholder token. The host stores but does not verify
    ///     it; real issuance happens out-of-band (installer / enrollment)
    ///     and the host looks it up. The agent sends a non-empty value
    ///     so the host's structural validation (non-empty join_token)
    ///     doesn't reject the join.
    /// </summary>
    public const string JoinTokenPlaceholder = "v0.1-unverified-token";

    /// <summary>
    ///     Run a single registration attempt. On success:
    ///   - <see cref="NodeAgentState.Current" /> is set to the new
    ///     identity.
    ///   - the cert directory is created (and
    ///     <see cref="NodeAgentOptions.Enrolled" /> flipped true).
    ///   - the caller logs "Joined as node {NodeId}".
    ///   On failure, the exception propagates and the caller decides
    ///   retry/backoff.
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task JoinOnceAsync(CancellationToken cancellationToken)
    {
        var request = new RegisterNodeRequest(
            JoinToken: JoinTokenPlaceholder,
            Hostname: config.Hostname,
            IpAddress: NodeJoinerFields.ResolveIpAddress(config.ControlPlaneUrl),
            Role: NodeRole.Compute,
            Hardware: NodeHardwareSpecBuilder.Build(
                config.CpuCores,
                config.RamBytes,
                config.DiskBytes),
            IsoVersion: NodeJoinerFields.ResolveIsoVersion(),
            WireguardPublicKey: string.Empty);

        var response = await transport.JoinAsync(request, cancellationToken);

        state.Current = new NodeIdentity(
            NodeId: response.NodeId,
            ClusterId: response.ClusterId,
            ClusterEndpoint: response.ClusterEndpoint,
            Cursor: 0);

        MtlsCertWriter.Persist(nodeOptions);

        logger.LogInformation(
            "Joined as node {NodeId} (cluster {ClusterId}, control plane {ControlPlaneUrl})",
            response.NodeId,
            response.ClusterId,
            response.ClusterEndpoint);
    }
}