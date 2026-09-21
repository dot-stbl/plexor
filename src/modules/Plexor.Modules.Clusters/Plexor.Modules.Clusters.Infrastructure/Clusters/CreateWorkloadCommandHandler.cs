// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateWorkloadCommandHandler — provision a new workload. Reserves
// org-scoped quota, inserts the row in Pending, then asks the
// IComputeProvider to create the VM. Pattern mirrors the rest of the
// workload write handlers (one file per command per folder-organization
// §1).
// ============================================================================

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
