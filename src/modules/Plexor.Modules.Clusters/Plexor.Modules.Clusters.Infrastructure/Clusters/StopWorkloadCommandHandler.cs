// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// StopWorkloadCommandHandler — gracefully stop a running workload.
// Pattern mirrors StartWorkloadCommandHandler: cluster DbContext +
// compute provider + mapper, lifecycle event written after the provider
// acks. One file per command per folder-organization §1.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Compute;

namespace Plexor.Modules.Clusters.Infrastructure.Clusters;

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
