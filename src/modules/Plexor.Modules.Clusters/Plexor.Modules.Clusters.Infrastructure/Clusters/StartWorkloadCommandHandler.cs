// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// StartWorkloadCommandHandler — start a previously provisioned workload.
// Pattern mirrors CreateWorkloadCommandHandler: cluster DbContext +
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
