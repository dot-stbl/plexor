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
        CommandEnvelope envelope, CancellationToken ct)
    {
        CreateWorkloadPayload payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<CreateWorkloadPayload>(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(envelope.PayloadJson)),
                cancellationToken: ct)
                ?? throw new InvalidOperationException("payload deserialized to null");
        }
        catch (Exception ex)
        {
            return ExecutorResult.Fail(
                $"workload.create payload parse failed: {ex.Message}");
        }

        var provider = registry.GetProvider(payload.Spec.Kind);
        if (provider is null)
        {
            return ExecutorResult.Fail(
                $"no provider registered for kind '{payload.Spec.Kind}'");
        }

        try
        {
            var workload = await provider.CreateAsync(payload.Spec, ct);
            logger.LogInformation(
                "Created workload {WorkloadId} ({Kind}) for {Name}",
                workload.Id, workload.Kind, workload.Name);
            return ExecutorResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Provider {Provider} failed creating {Kind} workload {Name}",
                provider.GetType().Name, payload.Spec.Kind, payload.Spec.Name);
            return ExecutorResult.Fail(
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}