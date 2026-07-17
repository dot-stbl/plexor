// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeCommandHandlers — NodeJoin / NodeHeartbeat / ListNodes. The
// NodeAgent-facing surface. NodeJoin is the only endpoint that runs
// without a node-bearer token (it authenticates via the one-time join
// token); the others require a valid node-bearer token.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Clusters.Infrastructure.Persistence.Specifications;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Mtls;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Persistence;
using Plexor.Shared.Workloads;

namespace Plexor.Modules.Clusters.Infrastructure.Clusters;

/// <summary>
///     Redeem a join token. Token lookup via Repository (read);
///     cluster status check via DbContext (conditional update shape);
///     node insert + token revocation via DbContext (writes).
/// </summary>
/// <param name="db">ClusterDbContext for writes + cluster lookup.</param>
/// <param name="tokenRepo">Read surface for token-by-hash lookup.</param>
public sealed class NodeJoinCommandHandler(
    ClusterDbContext db,
    Repository<JoinToken> tokenRepo,
    ICertificateAuthority caAuthority) : ICommandHandler<NodeJoinCommand, NodeJoinResult>
{
    /// <inheritdoc />
    public async Task<NodeJoinResult> HandleAsync(
        NodeJoinCommand command,
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(command.JoinToken))
        {
            throw new ClustersException(
                ClustersExceptions.InvalidJoinToken,
                "Join token is required.");
        }

        var tokenHash = await TokenHasher.HashAsync(command.JoinToken, cancellationToken);

        // Repository read: any row matching the hash (active or otherwise;
        // status check happens after fetch so we can distinguish
        // "revoked" from "never existed" with a single column lookup).
        if (await tokenRepo.FirstOrDefaultAsync(
                new JoinTokenByHashSpec(tokenHash),
                cancellationToken) is not { } token
            || token.Status != TokenStatus.Active)
        {
            throw new ClustersException(
                ClustersExceptions.InvalidJoinToken,
                "Join token is invalid, revoked, or expired.");
        }

        if (token.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new ClustersException(
                ClustersExceptions.InvalidJoinToken,
                "Join token is expired.");
        }

        if (token.IntendedRole != command.Role)
        {
            throw new ClustersException(
                ClustersExceptions.InvalidJoinToken,
                $"Token is for role '{token.IntendedRole}' but node requested '{command.Role}'.");
        }

        if (await db.Clusters
            .AsNoTracking()
            .Where(cluster => cluster.Id == token.ClusterId)
            .Select(cluster => new { cluster.Id, cluster.OrgId, cluster.Endpoint, cluster.Status })
            .FirstOrDefaultAsync(cancellationToken) is not { } cluster || cluster.Status == ClusterStatus.Offline)
        {
            throw new ClustersException(
                ClustersExceptions.ClusterNotFound,
                $"Cluster '{token.ClusterId}' not found or offline.");
        }

        if (await db.Nodes.AsNoTracking().AnyAsync(
                node => node.ClusterId == token.ClusterId && node.Hostname == command.Hostname,
                cancellationToken))
        {
            throw new ClustersException(
                ClustersExceptions.NodeHostnameTaken,
                $"Hostname '{command.Hostname}' is already taken in this cluster.");
        }

        var now = DateTimeOffset.UtcNow;
        var nodeId = IdGenerator.NewNodeId();
        var node = new Node
        {
            Id = nodeId,
            ClusterId = cluster.Id,
            OrgId = cluster.OrgId,
            Hostname = command.Hostname,
            Role = command.Role,
            Status = NodeStatus.Ready,
            Spec = command.Hardware,
            LastHeartbeatAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // Mark the join token as consumed (one-time use). The node id
        // is recorded for the audit trail. Tracked entity write (not
        // ExecuteUpdate) so the handler works on both npgsql + InMemory.
        db.Entry(token).Property(static jt => jt.Status).CurrentValue = TokenStatus.Revoked;
        db.Entry(token).Property(static jt => jt.RedeemedByNodeId).CurrentValue = nodeId;

        await db.Nodes.AddAsync(node, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // Node-bearer token (v0.1: opaque random; Phase 5+ signs a JWT
        // via the Sigil module). Returned once; NodeAgent persists it.
        var nodeToken = TokenHasher.NewSecret();

        // WireGuard config blob — v0.1 returns an empty placeholder.
        // WireGuard config blob — v0.1 returns an empty placeholder.
        // Phase 5+ generates a real wg-quick.conf once the mesh automation
        // is wired up (plan-clusters.md "Out of scope").
        var wireguardConfig = string.Empty;

        // mTLS triple: the host signs a fresh client cert for this
        // node using the Plexor CA, hands the cert + private key
        // + CA root PEM to the NodeAgent over the join response.
        // The NodeAgent writes cert+key to disk and pins the CA
        // root when verifying the host's server cert.
        //
        // Cert TTL is the configured CA lifetime (10y MVP, no
        // rotation) — the leaf lives as long as the CA that
        // signed it. If we ever introduce rotation the TTL here
        // would diverge from the CA's.
        var leafCert = caAuthority.IssueClientCert(
            X509Authority.BuildDn($"node_{nodeId}"),
            PlexorCertAuthorityInstaller.DefaultCaLifetime);

        var nodeCertPem = X509Authority.ToPem(leafCert);
        var nodeKeyPem = X509Authority.PrivateKeyToPem(leafCert);
        var caCertPem = X509Authority.ToPem(caAuthority.GetRootCertificate());
        leafCert.Dispose();

        return new NodeJoinResult(
            nodeId,
            cluster.Id,
            nodeToken,
            cluster.Endpoint,
            wireguardConfig,
            nodeCertPem,
            nodeKeyPem,
            caCertPem);
    }
}

/// <summary>
///     Stamp a node's heartbeat. Sets <see cref="Node.LastHeartbeatAt" />
///     to now + flips <see cref="Node.Status" /> to
///     <see cref="NodeStatus.Ready" />. Returns the cluster status so
///     NodeAgent can react (e.g. drain + exit on
///     <see cref="ClusterStatus.Offline" />). List nodes in one
///     cluster — see ClusterReadHandlers.cs (moved to
///     <see cref="ListNodesQueryHandler" /> for Repository pattern).
/// </summary>
/// <param name="db"></param>
public sealed class NodeHeartbeatCommandHandler(
    ClusterDbContext db) : ICommandHandler<NodeHeartbeatCommand, NodeHeartbeatResult>
{
    /// <inheritdoc />
    public async Task<NodeHeartbeatResult> HandleAsync(
        NodeHeartbeatCommand command,
        CancellationToken cancellationToken = default)
    {

        if (await db.Clusters
            .AsNoTracking()
            .Where(cluster => cluster.Id == command.ClusterId)
            .Select(cluster => (ClusterStatus?)cluster.Status)
            .FirstOrDefaultAsync(cancellationToken) is not { } clusterStatus)
        {
            throw new ClustersException(
                ClustersExceptions.ClusterNotFound,
                $"Cluster '{command.ClusterId}' not found.");
        }

        if (await db.Nodes.FirstOrDefaultAsync(
                node => node.Id == command.NodeId && node.ClusterId == command.ClusterId,
                cancellationToken) is not { } node)
        {
            throw new ClustersException(
                ClustersExceptions.NodeNotFound,
                $"Node '{command.NodeId}' in cluster '{command.ClusterId}' not found.");
        }

        var now = DateTimeOffset.UtcNow;
        db.Entry(node).Property(static n => n.LastHeartbeatAt).CurrentValue = now;
        // Don't flip a draining node back to Ready mid-drain — operators
        // want the drain to complete cleanly. Likewise an Offline cluster
        // means the host is unreachable; keep the node in its terminal state.
        if (node.Status != NodeStatus.Draining && clusterStatus != ClusterStatus.Offline)
        {
            db.Entry(node).Property(static n => n.Status).CurrentValue = NodeStatus.Ready;
        }
        db.Entry(node).Property(static n => n.Spec).CurrentValue = command.Hardware;
        db.Entry(node).Property(static n => n.UpdatedAt).CurrentValue = now;

        // Phase D Tier 4 — drift detection via heartbeat reports.
        // Each report is one workload the agent currently knows about.
        // We match by (cluster, node, name) so the agent doesn't need
        // to know the control-plane WorkloadId at this layer; Tier 5
        // actions carry LocalId so the WorkloadIdMap (in the agent)
        // can resolve start/stop/delete without exposing the
        // control-plane id to provider code.
        //
        // Lookup is one fetch — the cluster-scoped workloads filtered
        // by (node, name). For v0.1 the agent doesn't yet tag its
        // workloads with the assigned LocalId on create, so this
        // match-by-name path is the only reconciliation available.
        // Tier 5 will extend Workload.CreateAsync to write back the
        // LocalId, after which the lookup can resolve by name alone
        // with deterministic semantics.
        if (command.Reports.Count > 0)
        {
            await ReconcileWorkloadReportsAsync(command.Reports, command.ClusterId, command.NodeId, now, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new NodeHeartbeatResult(command.NodeId, clusterStatus, DateTimeOffset.UtcNow);
    }

    /// <summary>
    ///     Phase D Tier 4 — walk the agent's per-workload reports and
    ///     reconcile each one against the durable
    ///     <c>forge.workloads</c> view by (cluster, assigned node,
    ///     name). State is updated; a row that doesn't match is
    ///     drift that the operator should see in the UI — we
    ///     surface it as <see cref="ClustersExceptions.WorkloadNotFound" />
    ///     so the operator's audit log captures the anomaly rather
    ///     than silently ignoring it.
    /// </summary>
    /// <param name="reports">Agent's per-workload state snapshots.</param>
    /// <param name="clusterId">Cluster the agent belongs to.</param>
    /// <param name="nodeId">Calling node (drives the assigned-node filter).</param>
    /// <param name="now">Stamped onto <c>LastReportedAt</c> on every reconciled row.</param>
    /// <param name="cancellationToken"></param>
    private async Task ReconcileWorkloadReportsAsync(
        IReadOnlyList<WorkloadReport> reports,
        ClusterId clusterId,
        NodeId nodeId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Look up all (cluster, node, name) tuples in one round-trip
        // rather than N queries. Names are unique per cluster so we
        // don't expect collisions; the index is (cluster_id, name).
        var names = reports.Select(r => r.Name).ToArray();
        var matched = await db.Workloads
            .Where(w => w.ClusterId == clusterId
                        && w.AssignedNodeId == nodeId
                        && names.Contains(w.Name))
            .ToListAsync(cancellationToken);

        var byName = matched.ToDictionary(w => w.Name, StringComparer.Ordinal);
        foreach (var report in reports)
        {
            if (!byName.TryGetValue(report.Name, out var workload))
            {
                throw new ClustersException(
                    ClustersExceptions.WorkloadNotFound,
                    $"Drift: node {nodeId} reported workload '{report.Name}' "
                    + $"that has no row in forge.workloads for cluster {clusterId}.");
            }

            workload.State = MapReportState(report.State);
            workload.LastReportedAt = now;
            db.Entry(workload).Property(static w => w.UpdatedAt).CurrentValue = now;

            // v0.1 — Write the LocalId back when the agent first
            // reports a workload. Future Tier 5 work (action
            // commands) consumes this column when routing start /
            // stop / delete to the right runtime instance.
            if (!string.IsNullOrEmpty(report.LocalId)
                && string.IsNullOrEmpty(workload.LocalId))
            {
                workload.LocalId = report.LocalId;
            }
        }
    }

    /// <summary>
    ///     Translate the wire-stable <see cref="WorkloadReportState" />
    ///     (Plexor.Shared.NodeApi) onto the internal
    ///     <see cref="WorkloadState" /> (Plexor.Shared.Workloads).
    ///     Pure mapping; no I/O.
    /// </summary>
    /// <param name="state"></param>
    private static WorkloadState MapReportState(WorkloadReportState state)
    {
        return state switch
        {
            WorkloadReportState.Provisioning => WorkloadState.Provisioning,
            WorkloadReportState.Running => WorkloadState.Running,
            WorkloadReportState.Stopped => WorkloadState.Stopped,
            WorkloadReportState.Failed => WorkloadState.Failed,
            WorkloadReportState.Unknown => WorkloadState.Unknown,
            _ => WorkloadState.Unknown,
        };
    }
}


