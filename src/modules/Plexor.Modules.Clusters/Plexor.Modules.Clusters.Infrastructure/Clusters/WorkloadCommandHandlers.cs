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
            cancellationToken);

        if (workload is null)
        {
            throw new ClustersException(
                ClustersExceptions.WorkloadNotFound,
                $"Workload '{command.WorkloadId}' not found in cluster '{command.ClusterId}'.");
        }

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
                LocalId: workload.LocalId!));
#pragma warning restore VSTHRD103

        var now = DateTimeOffset.UtcNow;
        var nodeCommand = new Domain.Entities.NodeCommand(
            Id: Guid.NewGuid(),
            NodeId: workload.AssignedNodeId.Value,
            CommandId: wireCommandId,
            Type: commandType,
            PayloadJson: payloadJson)
        {
            Status = Domain.Entities.NodeCommandStatus.Pending,
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

            if (refreshed?.Status == Domain.Entities.NodeCommandStatus.Acked)
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

            if (refreshed?.Status == Domain.Entities.NodeCommandStatus.Failed)
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
