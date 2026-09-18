// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadCreateExecutor — ICommandExecutor for the wire type
// "workload.create". Deserializes the envelope's PayloadJson as a
// CreateWorkloadPayload, looks up the matching IWorkloadProvider by
// kind, and runs CreateAsync. Catches all exceptions and converts
// them to ExecutorResult.Fail so the dispatcher's exception
// handler doesn't double-handle.
// ============================================================================

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Plexor.NodeAgent.Abstractions;
using Plexor.NodeAgent.Telemetry;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Executors;

/// <summary>
///     Handles <c>workload.create</c> commands. The executor is
///     provider-agnostic — it looks the right <see cref="IWorkloadProvider" />
///     up by <see cref="WorkloadSpec.Kind" /> and delegates. Adding a new
///     kind means adding a new <see cref="IWorkloadProvider" />
///     registration, not a new executor.
/// </summary>
/// <param name="registry"></param>
/// <param name="logger"></param>
/// <remarks>
///     Build the executor. v0.1 ships with the
///     KVM/libvirt provider registered as the only provider.
/// </remarks>
public sealed class WorkloadCreateExecutor(
    IWorkloadRegistry registry,
    ILogger<WorkloadCreateExecutor> logger) : ICommandExecutor
{
    /// <inheritdoc />
    public string Type => CommandType.WorkloadCreate.Instance.Name;

    /// <inheritdoc />
    public async Task<ExecutorResult> ExecuteAsync(
        CommandEnvelope envelope,
        CancellationToken cancellationToken)
    {
        // OTel: host-side span wrapping the whole command
        // handler. Distinct from the provider's CreateAsync
        // span (which is nested under this one in the span
        // tree when both fire). Tags carry the wire-side
        // identity (command id, kind) so ops can correlate
        // //nodes/heartbeat, //nodes/command, and //vm spans.
        using var span = WorkloadTelemetry.ActivitySource.StartActivity(
            "Plexor.NodeAgent.Executor.workload.create",
            ActivityKind.Internal);
        span?.SetTag("command.id", envelope.CommandId.ToString());
        span?.SetTag("command.type", envelope.Type);

        try
        {
            if (await JsonSerializer.DeserializeAsync<CreateWorkloadPayload>(
                    new MemoryStream(Encoding.UTF8.GetBytes(envelope.PayloadJson)),
                    cancellationToken: cancellationToken)
                is not { } payload)
            {
                return ExecutorResult.Fail(
                    "workload.create payload deserialized to null");
            }

            span?.SetTag("workload.kind", payload.Spec.Kind.Name);
            span?.SetTag("workload.name", payload.Spec.Name);

            if (registry.GetProvider(payload.Spec.Kind) is not { } provider)
            {
                return ExecutorResult.Fail(
                    $"no provider registered for kind '{payload.Spec.Kind}'");
            }

            var workload = await provider.CreateAsync(payload.Spec, cancellationToken);
            logger.LogInformation(
                "Created workload {LocalId} ({Kind}) for {Name}",
                workload.Id,
                workload.Kind,
                workload.Name);

            span?.SetTag("workload.local_id", workload.Id.ToString());

            // Tier 5: hand the LocalId back to the dispatcher so the
            // control plane can persist it on the workload row
            // (forge.workloads.local_id) before the next heartbeat
            // arrives. Without this, the agent only had the value
            // locally and the control plane had to wait for a
            // heartbeat cycle to learn the runtime handle.
            return ExecutorResult.OkWithLocalId(workload.Id);
        }
        catch (Exception ex)
        {
            return ExecutorResult.Fail(
                $"workload.create exception: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
