// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadActionExecutor — single ICommandExecutor that handles the
// three workload lifecycle actions: workload.start, workload.stop,
// workload.delete. The wire type field on the envelope drives which
// provider method is called (StartAsync / StopAsync / DeleteAsync).
//
// One executor instead of three because:
//   - All three deserialize the same payload (WorkloadActionPayload)
//   - The provider methods all take (Guid workloadId, CancellationToken)
//   - Splitting them into three executors is boilerplate without
//     a behavior difference
// ============================================================================

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Plexor.NodeAgent.Abstractions;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Executors;

/// <summary>
/// Handles <c>workload.start</c>, <c>workload.stop</c>, and
/// <c>workload.delete</c>. The wire type on the envelope drives
/// which provider method is called.
/// </summary>
public sealed class WorkloadActionExecutor : ICommandExecutor
{
    private readonly IWorkloadRegistry registry;
    private readonly ILogger<WorkloadActionExecutor> logger;

    /// <summary>Build the executor. v0.1 ships with the
    /// KVM/libvirt provider registered as the only provider.</summary>
    public WorkloadActionExecutor(
        IWorkloadRegistry registry,
        ILogger<WorkloadActionExecutor> logger)
    {
        this.registry = registry;
        this.logger = logger;
    }

    /// <inheritdoc />
    public string Type => "workload.action";

    /// <summary>Dispatch on the envelope's actual type. The
    /// dispatcher uses <see cref="Type"/> for initial lookup;
    /// the executor then re-reads the envelope to find the
    /// concrete action (start / stop / delete).</summary>
    public async Task<ExecutorResult> ExecuteAsync(
        CommandEnvelope envelope, CancellationToken ct)
    {
        if (!TryPickAction(envelope.Type, out var action))
        {
            return ExecutorResult.Fail(
                $"workload.action: unknown action '{envelope.Type}'");
        }

        WorkloadActionPayload payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<WorkloadActionPayload>(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(envelope.PayloadJson)),
                cancellationToken: ct)
                ?? throw new InvalidOperationException("payload deserialized to null");
        }
        catch (Exception ex)
        {
            return ExecutorResult.Fail(
                $"workload.{action} payload parse failed: {ex.Message}");
        }

        // v0.1: the provider always lives at kind=Vm. Real impl
        // reads workloadId -> kind from a local map. (v0.2+
        // stores this map keyed by workload id and built at
        // create-time.)
        var provider = registry.GetProvider(new WorkloadKind.Vm());
        if (provider is null)
        {
            return ExecutorResult.Fail(
                "no VM provider registered (only VM workloads supported in v0.1)");
        }

        try
        {
            return action switch
            {
                "start" => await ExecuteStartAsync(provider, payload, envelope, ct),
                "stop" => await ExecuteStopAsync(provider, payload, envelope, ct),
                "delete" => await ExecuteDeleteAsync(provider, payload, envelope, ct),
                _ => ExecutorResult.Fail($"workload.action: unknown action '{action}'"),
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Provider {Provider} failed {Action} on workload {WorkloadId}",
                provider.GetType().Name, action, payload.WorkloadId);
            return ExecutorResult.Fail(
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool TryPickAction(string envelopeType, out string action)
    {
        switch (envelopeType)
        {
            case "workload.start":
                action = "start";
                return true;
            case "workload.stop":
                action = "stop";
                return true;
            case "workload.delete":
                action = "delete";
                return true;
            default:
                action = string.Empty;
                return false;
        }
    }

    private async Task<ExecutorResult> ExecuteStartAsync(
        IWorkloadProvider provider, WorkloadActionPayload payload,
        CommandEnvelope envelope, CancellationToken ct)
    {
        var workload = await provider.StartAsync(payload.WorkloadId, ct);
        logger.LogInformation(
            "Started workload {WorkloadId} for command {CommandId}",
            workload.Id, envelope.CommandId);
        return ExecutorResult.Ok();
    }

    private async Task<ExecutorResult> ExecuteStopAsync(
        IWorkloadProvider provider, WorkloadActionPayload payload,
        CommandEnvelope envelope, CancellationToken ct)
    {
        var workload = await provider.StopAsync(payload.WorkloadId, ct);
        logger.LogInformation(
            "Stopped workload {WorkloadId} for command {CommandId}",
            workload.Id, envelope.CommandId);
        return ExecutorResult.Ok();
    }

    private async Task<ExecutorResult> ExecuteDeleteAsync(
        IWorkloadProvider provider, WorkloadActionPayload payload,
        CommandEnvelope envelope, CancellationToken ct)
    {
        var workload = await provider.DeleteAsync(payload.WorkloadId, ct);
        logger.LogInformation(
            "Deleted workload {WorkloadId} for command {CommandId}",
            workload.Id, envelope.CommandId);
        return ExecutorResult.Ok();
    }
}