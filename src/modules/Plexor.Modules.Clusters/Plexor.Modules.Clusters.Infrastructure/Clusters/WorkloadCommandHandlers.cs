// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Workload write-handlers — Create / Delete. Co-located in one file
// because every handler depends on the same ClusterDbContext, the
// IWorkloadMapper, and the bodies are < 80 lines each. Pattern
// mirrors ClusterCommandHandlers.
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Mappers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Clusters.Infrastructure.Clusters;

/// <summary>
///     Provision a new workload. Starts at
///     <see cref="Plexor.Shared.Workloads.WorkloadState.Provisioning" />;
///     the NodeAgent's drift-detection job (Phase D Tier 4) reports
///     the runtime-side state back as the local lifecycle
///     progresses. Returns the durable
///     <see cref="WorkloadSummary" /> so the operator's
///     <c>POST</c> response carries the wire id.
/// </summary>
/// <param name="db">EF Core context for the write.</param>
/// <param name="mapper">Entity → DTO mapper (Mapperly-generated).</param>
public sealed class CreateWorkloadCommandHandler(
    ClusterDbContext db,
    IWorkloadMapper mapper) : ICommandHandler<CreateWorkloadCommand, WorkloadSummary>
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
            State = Plexor.Shared.Workloads.WorkloadState.Provisioning,
            LastMessage = null,
            LastReportedAt = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await db.Workloads.AddAsync(workload, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return mapper.ToSummary(workload);
    }
}

/// <summary>
///     Soft-delete a workload. The NodeAgent's next drift poll
///     tears down the local runtime handle; the control-plane
///     row stays in <c>forge.workloads</c> for audit + FK integrity.
/// </summary>
/// <param name="db">EF Core context for the write.</param>
public sealed class DeleteWorkloadCommandHandler(
    ClusterDbContext db) : ICommandHandler<DeleteWorkloadCommand, Unit>
{
    /// <inheritdoc />
    public async Task<Unit> HandleAsync(
        DeleteWorkloadCommand command,
        CancellationToken cancellationToken = default)
    {
        var workload = await db.Workloads.FirstOrDefaultAsync(
            w => w.ClusterId == command.ClusterId && w.Id == command.WorkloadId,
            cancellationToken);

        if (workload is null)
        {
            throw new ClustersException(
                ClustersExceptions.WorkloadNotFound,
                $"Workload '{command.WorkloadId}' not found in cluster '{command.ClusterId}'.");
        }

        db.Workloads.Remove(workload);
        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
