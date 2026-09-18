// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Workload write-handlers — Create / Start / Stop / Delete. Co-located in
// one file because every handler depends on the same ClusterDbContext, the
// IWorkloadMapper, and the IComputeProvider; bodies are < 80 lines each.
// Pattern mirrors ClusterCommandHandlers.
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Kernel.Compute;
using Plexor.Shared.Kernel.Identity;
using Plexor.Shared.Kernel.Quotas;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Clusters;

/// <summary>
///     Provision a new workload. Reserves quota, inserts the row
///     in <see cref="WorkloadLifecycleState.Pending" />, then asks
///     the <see cref="IComputeProvider" /> to create the VM. On a
///     successful provider ack, transitions the workload through
///     <c>Provisioning → Stopped</c> (the VM exists but is not yet
///     powered on — power-on is a separate Start command). On
///     provider failure, marks the workload
///     <see cref="WorkloadLifecycleState.Failed" /> with the
///     provider's reason and surfaces a typed
///     <see cref="ClustersException" /> to the operator.
/// </summary>
/// <param name="db">EF Core context for the resource-create transaction.</param>
/// <param name="mapper">Entity → DTO mapper (Mapperly-generated).</param>
/// <param name="computeProvider">Compute backend that owns the VM lifecycle.</param>
/// <param name="quotaEnforcer">
///     Reserves <c>compute.workloads.count</c> capacity inside the same
///     transaction as the workload INSERT.
/// </param>
/// <param name="currentUser">
///     Caller identity forwarded into <see cref="QuotaScope" /> so
///     the quota audit emitter can attach the actor.
/// </param>
/// <remarks>
///     <para><b>OrgId lookup.</b> The quota is org-scoped (the
///     workload catalog counts at the org level, not per cluster), so
///     the handler fetches the parent cluster's <c>OrgId</c> via a
///     single-column SELECT before calling the enforcer. A missing
///     parent cluster throws <see cref="ClustersExceptions.ClusterNotFound" />
///     before any quota is consumed.</para>
///     <para><b>Why a transaction wraps the row INSERT (not the
///     provider call).</b> The quota reservation + workload INSERT
///     must commit atomically. The provider call is OUTSIDE the
///     transaction — the provider manages its own resources
///     (libvirt / k3s / ...) and can take seconds. A long-running
///     provider call holding a Postgres advisory lock would starve
///     every other quota enforcer on the cluster.</para>
///     <para><b>Provider call failure path.</b> If <see cref="IComputeProvider.CreateVmAsync" />
///     throws <see cref="ComputeProviderException" />, the workload row
///     is already persisted. We mark it <c>Failed</c> + append a
///     lifecycle event with the provider's reason, then rethrow as
///     a <see cref="ClustersException" /> with
///     <see cref="ClustersExceptions.ComputeProviderFailed" /> so the
///     operator sees a 502 with the typed failure code (not a raw
///     provider stack). The row stays for operator inspection /
///     manual cleanup.</para>
/// </remarks>
public sealed class CreateWorkloadCommandHandler(
    ClusterDbContext db,
    IWorkloadMapper mapper,
    IComputeProvider computeProvider,
    IQuotaEnforcer quotaEnforcer,
    ICurrentUser currentUser) : ICommandHandler<CreateWorkloadCommand, WorkloadSummary>
{
    /// <inheritdoc />
    public async Task<WorkloadSummary> HandleAsync(
        CreateWorkloadCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ClustersException(
                ClustersExceptions.InvalidWorkloadSpec,
                "Workload name is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Kind))
        {
            throw new ClustersException(
                ClustersExceptions.InvalidWorkloadSpec,
                "Workload kind is required.");
        }

        // Single transaction: enforcer UPDATE on quotas.quota_usage +
        // workload INSERT commit (or roll back) together. Skipped
        // against non-relational providers (e.g. the InMemory
        // provider used by handler unit tests) — production runs
        // always go through the transactional path.
        await using var transaction =
            await db.Database.BeginTransactionIfSupportedAsync(cancellationToken);

        // Resolve the cluster's OrgId for the org-scoped quota lookup.
        // The Workload row holds cluster_id but no FK (forge schema
        // doesn't enforce it — see WorkloadConfiguration); fetching the
        // parent cluster's OrgId also serves as a sanity check that
        // the cluster exists before we burn quota.
        var clusterOrgId = await db.Clusters.AsNoTracking()
            .Where(cluster => cluster.Id == command.ClusterId)
            .Select(static cluster => (Guid?)cluster.OrgId)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new ClustersException(
                ClustersExceptions.ClusterNotFound,
                $"Cluster '{command.ClusterId}' not found.");

        // Pre-check + reserve compute.workloads.count at the org scope.
        // ActorUserId flows into the QuotaScope so the audit emitter
        // can attach the caller to any UsageExceeded / LimitApproaching
        // event.
        var quotaCheck = await quotaEnforcer.CheckAndReserveAsync(
            QuotaScope.Org(clusterOrgId, currentUser.UserId),
            QuotaDefinitionKey.WorkloadsCount,
            amount: 1,
            cancellationToken);

        if (quotaCheck is QuotaCheckResult.Denied denied)
        {
            throw new QuotaExceededException(
                denied.Limit,
                denied.Used,
                denied.Requested);
        }

        // Uniqueness check now runs inside the transaction so a
        // duplicate-name collision rolls back the reserved quota.
        if (await db.Workloads.AsNoTracking().AnyAsync(
                w => w.ClusterId == command.ClusterId && w.Name == command.Name,
                cancellationToken))
        {
            throw new ClustersException(
                ClustersExceptions.InvalidWorkloadSpec,
                $"A workload named '{command.Name}' already exists in this cluster.");
        }

        var now = DateTimeOffset.UtcNow;
        var workload = new Workload
        {
            Id = IdGenerator.NewWorkloadId(),
            ClusterId = command.ClusterId,
            AssignedNodeId = null,
            LocalId = null,
            Name = command.Name,
            Kind = command.Kind,
            SpecJson = command.SpecJson,
            State = Shared.Workloads.WorkloadState.Provisioning,
            LifecycleState = WorkloadLifecycleState.Pending,
            ProviderVmId = null,
            LastMessage = null,
            LastReportedAt = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await db.Workloads.AddAsync(workload, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        // Provider call runs OUTSIDE the transaction (see remarks).
        // On failure, mark Failed + surface a typed exception.
        try
        {
            var providerVmId = await computeProvider.CreateVmAsync(
                new CreateVmRequest(
                    Name: command.Name,
                    Vcpu: 1,
                    MemoryBytes: 1024L * 1024L * 1024L,
                    DiskPaths: [],
                    NetworkNames: ["default"]),
                cancellationToken);

            // Pending → Provisioning (provider handle stored).
            var provisioningEvent = workload.MarkProvisioning(providerVmId, DateTimeOffset.UtcNow);
            await db.WorkloadLifecycleEvents.AddAsync(provisioningEvent, cancellationToken);

            // Provisioning → Stopped (VM exists but not powered on —
            // Start is a separate command from the operator).
            var stoppedEvent = workload.MarkStopped(DateTimeOffset.UtcNow);
            await db.WorkloadLifecycleEvents.AddAsync(stoppedEvent, cancellationToken);

            db.Entry(workload).Property(static w => w.UpdatedAt).CurrentValue = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (ComputeProviderException providerEx)
        {
            // The row stays in Failed state for operator inspection.
            // The reason is persisted on the LastMessage column + the
            // WorkloadLifecycleEvent audit row, so the operator can
            // diagnose without digging into logs.
            var failedEvent = workload.MarkFailed(providerEx.Message, DateTimeOffset.UtcNow);
            await db.WorkloadLifecycleEvents.AddAsync(failedEvent, CancellationToken.None);

            db.Entry(workload).Property(static w => w.UpdatedAt).CurrentValue = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);

            throw new ClustersException(
                ClustersExceptions.ComputeProviderFailed,
                $"Compute provider rejected workload '{workload.Name}': {providerEx.Message}",
                providerEx);
        }

        return mapper.ToSummary(workload);
    }
}

/// <summary>
///     Start a previously provisioned workload. The handler looks
///     up the workload's <c>provider_vm_id</c>, calls
///     <see cref="IComputeProvider.StartVmAsync" />, then transitions
///     <see cref="WorkloadLifecycleState.Stopped" /> →
///     <see cref="WorkloadLifecycleState.Running" />. Throws on a
///     workload in any state that doesn't permit Start
///     (<c>Deleted</c>, <c>Pending</c>, <c>Provisioning</c>).
/// </summary>
/// <param name="db">EF Core context for the read + lifecycle-event write.</param>
/// <param name="computeProvider">Compute backend that owns the VM lifecycle.</param>
/// <param name="mapper">Entity → DTO mapper for the result envelope.</param>
public sealed class StartWorkloadCommandHandler(
    ClusterDbContext db,
    IComputeProvider computeProvider,
    IWorkloadMapper mapper) : ICommandHandler<StartWorkloadCommand, WorkloadLifecycleResult>
{
    /// <inheritdoc />
    public async Task<WorkloadLifecycleResult> HandleAsync(
        StartWorkloadCommand command,
        CancellationToken cancellationToken = default)
    {
        var workload = await db.Workloads.FirstOrDefaultAsync(
            w => w.ClusterId == command.ClusterId && w.Id == command.WorkloadId,
            cancellationToken) ?? throw new ClustersException(
                ClustersExceptions.WorkloadNotFound,
                $"Workload '{command.WorkloadId}' not found in cluster '{command.ClusterId}'.");

        if (workload.ProviderVmId is null)
        {
            throw new ClustersException(
                ClustersExceptions.WorkloadNotFound,
                $"Workload '{command.WorkloadId}' has no provider_vm_id — Create never confirmed.");
        }

        // MarkRunning validates the transition (Stopped → Running is
        // the only legal source). Invalid transitions throw before
        // we touch the provider.
        var runningEvent = workload.MarkRunning(DateTimeOffset.UtcNow);
        await db.WorkloadLifecycleEvents.AddAsync(runningEvent, cancellationToken);

        try
        {
            await computeProvider.StartVmAsync(workload.ProviderVmId, cancellationToken);
        }
        catch (ComputeProviderException providerEx)
        {
            var failedEvent = workload.MarkFailed(providerEx.Message, DateTimeOffset.UtcNow);
            await db.WorkloadLifecycleEvents.AddAsync(failedEvent, CancellationToken.None);

            db.Entry(workload).Property(static w => w.UpdatedAt).CurrentValue = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);

            throw new ClustersException(
                ClustersExceptions.ComputeProviderFailed,
                $"Compute provider rejected Start on workload '{workload.Name}': {providerEx.Message}",
                providerEx);
        }

        db.Entry(workload).Property(static w => w.UpdatedAt).CurrentValue = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new WorkloadLifecycleResult(mapper.ToSummary(workload));
    }
}

/// <summary>
///     Gracefully stop a running workload. The handler calls
///     <see cref="IComputeProvider.StopVmAsync" />, then transitions
///     <see cref="WorkloadLifecycleState.Running" /> →
///     <see cref="WorkloadLifecycleState.Stopped" />. Resources
///     stay allocated (the workload is stopped, not deleted). Throws
///     on a workload in any state that doesn't permit Stop
///     (<c>Deleted</c>, <c>Pending</c>, <c>Provisioning</c>,
///     <c>Stopped</c>).
/// </summary>
/// <param name="db">EF Core context for the read + lifecycle-event write.</param>
/// <param name="computeProvider">Compute backend that owns the VM lifecycle.</param>
/// <param name="mapper">Entity → DTO mapper for the result envelope.</param>
public sealed class StopWorkloadCommandHandler(
    ClusterDbContext db,
    IComputeProvider computeProvider,
    IWorkloadMapper mapper) : ICommandHandler<StopWorkloadCommand, WorkloadLifecycleResult>
{
    /// <inheritdoc />
    public async Task<WorkloadLifecycleResult> HandleAsync(
        StopWorkloadCommand command,
        CancellationToken cancellationToken = default)
    {
        var workload = await db.Workloads.FirstOrDefaultAsync(
            w => w.ClusterId == command.ClusterId && w.Id == command.WorkloadId,
            cancellationToken) ?? throw new ClustersException(
                ClustersExceptions.WorkloadNotFound,
                $"Workload '{command.WorkloadId}' not found in cluster '{command.ClusterId}'.");

        if (workload.ProviderVmId is null)
        {
            throw new ClustersException(
                ClustersExceptions.WorkloadNotFound,
                $"Workload '{command.WorkloadId}' has no provider_vm_id — Create never confirmed.");
        }

        // MarkStopped validates the transition (Running → Stopped is
        // the only legal source state for a runtime-driven stop).
        var stoppedEvent = workload.MarkStopped(DateTimeOffset.UtcNow);
        await db.WorkloadLifecycleEvents.AddAsync(stoppedEvent, cancellationToken);

        try
        {
            await computeProvider.StopVmAsync(workload.ProviderVmId, cancellationToken);
        }
        catch (ComputeProviderException providerEx)
        {
            var failedEvent = workload.MarkFailed(providerEx.Message, DateTimeOffset.UtcNow);
            await db.WorkloadLifecycleEvents.AddAsync(failedEvent, CancellationToken.None);

            db.Entry(workload).Property(static w => w.UpdatedAt).CurrentValue = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);

            throw new ClustersException(
                ClustersExceptions.ComputeProviderFailed,
                $"Compute provider rejected Stop on workload '{workload.Name}': {providerEx.Message}",
                providerEx);
        }

        db.Entry(workload).Property(static w => w.UpdatedAt).CurrentValue = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new WorkloadLifecycleResult(mapper.ToSummary(workload));
    }
}

/// <summary>
///     Tear down a workload's VM + transition the row to
///     <see cref="WorkloadLifecycleState.Deleted" />. The handler calls
///     <see cref="IComputeProvider.DeleteVmAsync" /> on the workload's
///     <c>provider_vm_id</c>, then transitions through
///     <c>* → Deleting → Deleted</c>. The row is preserved (not
///     hard-deleted) for audit + reconciliation — the
///     <c>forge.workloads</c> table keeps the operator-visible
///     record of "this workload existed and was torn down at T".
///     Throws on a workload already in
///     <see cref="WorkloadLifecycleState.Deleted" />.
/// </summary>
/// <param name="db">EF Core context for the read + lifecycle-event write.</param>
/// <param name="computeProvider">Compute backend that owns the VM lifecycle.</param>
public sealed class DeleteWorkloadCommandHandler(
    ClusterDbContext db,
    IComputeProvider computeProvider) : ICommandHandler<DeleteWorkloadCommand, Unit>
{
    /// <inheritdoc />
    public async Task<Unit> HandleAsync(
        DeleteWorkloadCommand command,
        CancellationToken cancellationToken = default)
    {
        var workload = await db.Workloads.FirstOrDefaultAsync(
            w => w.ClusterId == command.ClusterId && w.Id == command.WorkloadId,
            cancellationToken) ?? throw new ClustersException(
                ClustersExceptions.WorkloadNotFound,
                $"Workload '{command.WorkloadId}' not found in cluster '{command.ClusterId}'.");

        // MarkDeleting validates the transition. Legal source
        // states: Stopped / Running / Failed. Deleted is terminal.
        var deletingEvent = workload.MarkDeleting(DateTimeOffset.UtcNow);
        await db.WorkloadLifecycleEvents.AddAsync(deletingEvent, cancellationToken);

        try
        {
            if (workload.ProviderVmId is not null)
            {
                await computeProvider.DeleteVmAsync(workload.ProviderVmId, cancellationToken);
            }
        }
        catch (ComputeProviderException providerEx)
        {
            var failedEvent = workload.MarkFailed(providerEx.Message, DateTimeOffset.UtcNow);
            await db.WorkloadLifecycleEvents.AddAsync(failedEvent, CancellationToken.None);

            db.Entry(workload).Property(static w => w.UpdatedAt).CurrentValue = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);

            throw new ClustersException(
                ClustersExceptions.ComputeProviderFailed,
                $"Compute provider rejected Delete on workload '{workload.Name}': {providerEx.Message}",
                providerEx);
        }

        var deletedEvent = workload.MarkDeleted(DateTimeOffset.UtcNow);
        await db.WorkloadLifecycleEvents.AddAsync(deletedEvent, cancellationToken);

        db.Entry(workload).Property(static w => w.UpdatedAt).CurrentValue = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
///     Enqueue a <see cref="WorkloadAction" /> against an existing
///     workload. Writes the matching <c>forge.commands</c> row,
///     then polls the row's status (Pending → Sent → Acked/Failed)
///     until the NodeAgent posts back a result. Returns the
///     post-action <see cref="Plexor.Shared.Workloads.WorkloadState" />.
///     v0.1 uses short-poll with a 30-second timeout (the agent's
///     own long-poll interval is 5s, so the realistic round-trip
///     latency is &lt; 10s; we cap at 30s to fail fast on
///     unreachable nodes).
/// </summary>
/// <param name="db">EF Core context for the write + read.</param>
public sealed class WorkloadActionCommandHandler(
    ClusterDbContext db) : ICommandHandler<WorkloadActionCommand, WorkloadActionResult>
{
    /// <summary>Total wait for the agent to acknowledge the action before failing fast.</summary>
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Polling interval between status checks.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc />
    public async Task<WorkloadActionResult> HandleAsync(
        WorkloadActionCommand command,
        CancellationToken cancellationToken = default)
    {
        // Load the workload — we need its LocalId (which the
        // agent reported back via heartbeat reconciliation) to
        // build the WorkloadActionPayload. If the workload hasn't
        // been provisioned yet, fail fast — the operator
        // should run workload.create first.
        var workload = await db.Workloads.FirstOrDefaultAsync(
            w => w.ClusterId == command.ClusterId && w.Id == command.WorkloadId,
            cancellationToken) ?? throw new ClustersException(
                ClustersExceptions.WorkloadNotFound,
                $"Workload '{command.WorkloadId}' not found in cluster '{command.ClusterId}'.");
        if (string.IsNullOrEmpty(workload.LocalId))
        {
            throw new ClustersException(
                ClustersExceptions.WorkloadNotFound,
                $"Workload '{command.WorkloadId}' has no LocalId yet; "
                + "wait for the first heartbeat before invoking an action.");
        }

        if (workload.AssignedNodeId is null)
        {
            throw new ClustersException(
                ClustersExceptions.WorkloadNotFound,
                $"Workload '{command.WorkloadId}' has no assigned node.");
        }

        // Enqueue the command. The wire command type is one of
        // the agent's known types (workload.start / .stop / .start
        // for restart). We synthesize a stable wire CommandId
        // (UUIDv7) so the agent's long-poll can correlate results
        // even after retries.
        var wireCommandId = Guid.NewGuid();

        var commandType = command.Action switch
        {
            WorkloadAction.Start => "workload.start",
            WorkloadAction.Stop => "workload.stop",
            WorkloadAction.Restart => "workload.start",  // restart = start after stop; the agent handles the pair
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.Action, null)
        };

        // VSTHRD103 false-positive — JsonSerializer.Serialize is
        // pure CPU-bound (no I/O on a string); the async overload
        // (SerializeAsync) requires a stream/pipeWriter and is
        // strictly heavier for a small payload. Suppress with
        // explanation rather than route through SerializeAsync.
#pragma warning disable VSTHRD103 // Sync serialize — no I/O on a small string payload.
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(
            new Plexor.Shared.NodeApi.WorkloadActionPayload(
                LocalId: workload.LocalId));
#pragma warning restore VSTHRD103

        var now = DateTimeOffset.UtcNow;
        var nodeCommand = new Domain.Entities.NodeCommand(
            Id: Guid.NewGuid(),
            NodeId: workload.AssignedNodeId.Value,
            CommandId: wireCommandId,
            Type: commandType,
            PayloadJson: payloadJson)
        {
            Status = NodeCommandStatus.Pending,
            CreatedAt = now
        };

        await db.Commands.AddAsync(nodeCommand, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // Short-poll the row's status. Real production would be
        // a long-poll via the agent's existing transport — for
        // MVP we just spin on the local DB which gives the agent
        // enough time (its 5s long-poll) to pick the command up.
        var deadline = DateTimeOffset.UtcNow + AckTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(PollInterval, cancellationToken);

            db.ChangeTracker.Clear();
            var refreshed = await db.Commands
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CommandId == wireCommandId, cancellationToken);

            if (refreshed?.Status == NodeCommandStatus.Acked)
            {
                // Tier 4 reconciliation: the heartbeat updates the
                // workload's State on the next tick. We trust the
                // command's result via the workload row, not the
                // command's ResultJson (the shape of which depends
                // on the wire type).
                var updated = await db.Workloads.AsNoTracking()
                    .FirstOrDefaultAsync(w => w.Id == command.WorkloadId, cancellationToken);
                return new WorkloadActionResult(
                    wireCommandId,
                    updated?.State ?? workload.State);
            }

            if (refreshed?.Status == NodeCommandStatus.Failed)
            {
                var msg = refreshed.ResultJson ?? "command failed without error message";
                throw new InvalidOperationException(
                    $"WorkloadAction '{command.Action}' failed: {msg}");
            }
        }

        throw new TimeoutException(
            $"WorkloadAction '{command.Action}' on workload '{command.WorkloadId}' did not complete within {AckTimeout.TotalSeconds:F0}s");
    }
}
