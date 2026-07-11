// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadCreateExecutor — ICommandExecutor for the wire type
// "workload.create". Deserializes the envelope's PayloadJson as a
// CreateWorkloadPayload, looks up the matching IWorkloadProvider by
// kind, and runs CreateAsync. Catches all exceptions and converts
// them to ExecutorResult.Fail so the dispatcher's exception
// handler doesn't double-handle.
// ============================================================================

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Plexor.NodeAgent.Abstractions;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Executors;

/// <summary>
/// Handles <c>workload.create</c> commands. The executor is
/// provider-agnostic — it looks the right <see cref="IWorkloadProvider"/>
/// up by <see cref="WorkloadSpec.Kind"/> and delegates. Adding a new
/// kind means adding a new <see cref="IWorkloadProvider"/>
/// registration, not a new executor.
/// </summary>
public sealed class WorkloadCreateExecutor : ICommandExecutor
{
    private readonly IWorkloadRegistry registry;
    private readonly ILogger<WorkloadCreateExecutor> logger;

    /// <summary>Build the executor. v0.1 ships with the
    /// KVM/libvirt provider registered as the only provider.</summary>
    public WorkloadCreateExecutor(
        IWorkloadRegistry registry,
        ILogger<WorkloadCreateExecutor> logger)
    {
        this.registry = registry;
        this.logger = logger;
    }

    /// <inheritdoc />
    public string Type => "workload.create";

    /// <inheritdoc />
    public async Task<ExecutorResult> ExecuteAsync(
        CommandEnvelope envelope, CancellationToken cancellationToken)
    {
        try
        {
            if (await JsonSerializer.DeserializeAsync<CreateWorkloadPayload>(
                    new MemoryStream(System.Text.Encoding.UTF8.GetBytes(envelope.PayloadJson)),
                    cancellationToken: cancellationToken)
                is not { } payload)
            {
                return ExecutorResult.Fail(
                    "workload.create payload deserialized to null");
            }

            if (registry.GetProvider(payload.Spec.Kind) is not { } provider)
            {
                return ExecutorResult.Fail(
                    $"no provider registered for kind '{payload.Spec.Kind}'");
            }

            var workload = await provider.CreateAsync(payload.Spec, cancellationToken);
            logger.LogInformation(
                "Created workload {WorkloadId} ({Kind}) for {Name}",
                workload.Id, workload.Kind, workload.Name);
            return ExecutorResult.Ok();
        }
        catch (Exception ex)
        {
            return ExecutorResult.Fail(
                $"workload.create exception: {ex.GetType().Name}: {ex.Message}");
        }
    }
}