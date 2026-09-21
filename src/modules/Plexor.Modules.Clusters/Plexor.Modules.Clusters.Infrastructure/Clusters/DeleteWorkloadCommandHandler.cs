// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DeleteWorkloadCommandHandler — tear down a workload's VM + transition
// the row to Deleted. Pattern mirrors Start/Stop: cluster DbContext +
// compute provider, lifecycle events written around the provider call.
// One file per command per folder-organization §1.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Compute;

namespace Plexor.Modules.Clusters.Infrastructure.Clusters;

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
