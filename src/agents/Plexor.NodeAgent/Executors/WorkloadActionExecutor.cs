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

using System.Text;
using System.Text.Json;
using Plexor.NodeAgent.Abstractions;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Executors;

/// <summary>
///     Handles <c>workload.start</c>, <c>workload.stop</c>, and
///     <c>workload.delete</c>. The wire type on the envelope drives
///     which provider method is called.
/// </summary>
/// <param name="registry"></param>
/// <param name="logger"></param>
/// <remarks>
///     Build the executor. v0.1 ships with the
///     KVM/libvirt provider registered as the only provider.
/// </remarks>
public sealed class WorkloadActionExecutor(
    IWorkloadRegistry registry,
    ILogger<WorkloadActionExecutor> logger) : ICommandExecutor
{
    /// <inheritdoc />
    public string Type => "workload.action";

    /// <summary>
    ///     Dispatch on the envelope's actual type. The
    ///     dispatcher uses <see cref="Type" /> for initial lookup;
    ///     the executor then re-reads the envelope to find the
    ///     concrete action (start / stop / delete).
    /// </summary>
    /// <param name="envelope"></param>
    /// <param name="cancellationToken"></param>
    public async Task<ExecutorResult> ExecuteAsync(
        CommandEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (!TryPickAction(envelope.Type, out var action))
        {
            return ExecutorResult.Fail(
                $"workload.action: unknown action '{envelope.Type}'");
        }

        try
        {
            if (await JsonSerializer.DeserializeAsync<WorkloadActionPayload>(
                    new MemoryStream(Encoding.UTF8.GetBytes(envelope.PayloadJson)),
                    cancellationToken: cancellationToken)
                is not { } payload)
            {
                return ExecutorResult.Fail(
                    $"workload.{action} payload deserialized to null");
            }

            // v0.1: the provider always lives at kind=Vm. Real impl
            // reads workloadId -> kind from a local map. (v0.2+
            // stores this map keyed by workload id and built at
            // create-time.)
            if (registry.GetProvider(new WorkloadKind.Vm()) is not { } provider)
            {
                return ExecutorResult.Fail(
                    "no VM provider registered (only VM workloads supported in v0.1)");
            }

            return action switch
            {
                "start" => await ExecuteStartAsync(provider, payload, envelope, cancellationToken),
                "stop" => await ExecuteStopAsync(provider, payload, envelope, cancellationToken),
                "delete" => await ExecuteDeleteAsync(provider, payload, envelope, cancellationToken),
                _ => ExecutorResult.Fail($"workload.action: unknown action '{action}'")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Provider {Provider} failed {Action} on workload {WorkloadId}",
                registry.GetType().Name,
                action, /* payload out of scope */
                Guid.Empty);

            return ExecutorResult.Fail(
                $"workload.{action} exception: {ex.GetType().Name}: {ex.Message}");
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
        IWorkloadProvider provider,
        WorkloadActionPayload payload,
        CommandEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var workload = await provider.StartAsync(Guid.Parse(payload.LocalId), cancellationToken);
        logger.LogInformation(
            "Started workload {WorkloadId} for command {CommandId}",
            workload.Id,
            envelope.CommandId);

        return ExecutorResult.Ok();
    }

    private async Task<ExecutorResult> ExecuteStopAsync(
        IWorkloadProvider provider,
        WorkloadActionPayload payload,
        CommandEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var workload = await provider.StopAsync(Guid.Parse(payload.LocalId), cancellationToken);
        logger.LogInformation(
            "Stopped workload {WorkloadId} for command {CommandId}",
            workload.Id,
            envelope.CommandId);

        return ExecutorResult.Ok();
    }

    private async Task<ExecutorResult> ExecuteDeleteAsync(
        IWorkloadProvider provider,
        WorkloadActionPayload payload,
        CommandEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var workload = await provider.DeleteAsync(Guid.Parse(payload.LocalId), cancellationToken);
        logger.LogInformation(
            "Deleted workload {WorkloadId} for command {CommandId}",
            workload.Id,
            envelope.CommandId);

        return ExecutorResult.Ok();
    }
}
