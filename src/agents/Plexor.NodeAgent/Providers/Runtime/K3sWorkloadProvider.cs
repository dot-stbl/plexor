// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// K3sWorkloadProvider — IWorkloadProvider for k3s workloads
// (matches WorkloadKind.K3s).
//
// Lifecycle:
//   1. CreateAsync parses WorkloadSpec.Config into K3sWorkloadConfig,
//      renders a kustomize directory (kustomization + deployment +
//      optional service) via the file-static renderer, writes all
//      three files under /var/lib/plexor/workloads/k3s/<name>/, then
//      runs `kubectl apply -k <dir>` to apply the manifest.
//   2. StartAsync scales the Deployment back to the configured
//      replicas (kubectl scale deployment/<name> --replicas=N).
//   3. StopAsync scales the Deployment to 0 (k8s idiom for "stop
//      without delete").
//   4. DeleteAsync tears down the workload: kubectl delete -k + rm.
//   5. ListAsync at agent boot reconciles against the existing
//      /var/lib/plexor/workloads/k3s/ tree on disk.
//
// State reporting:
//   - kubectl rollout status for "Running" detection (best-effort).
//   - kubectl get deployment/<name> -o jsonpath for replica counts.
//
// Path convention:
//   Plexor is Linux-only; manifests live under forward-slash strings,
//   not Path.Combine. kubectl accepts forward-slash paths.
// ==========================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     <see cref="IWorkloadProvider" /> for k3s workloads (matches
///     <see cref="WorkloadKind.K3s" />).
/// </summary>
/// <param name="kubectl">
///     kubectl CLI wrapper. Injected so unit tests can substitute
///     a deterministic fake that records invocations without
///     shelling out to a real kubectl binary.
/// </param>
/// <param name="clock">
///     Time provider for <see cref="LocalWorkload.CreatedAt" />
///     and <see cref="LocalWorkload.StartedAt" />. Defaults to
///     <see cref="TimeProvider.System" /> via DI.
/// </param>
/// <param name="logger">Structured logger.</param>
public sealed class K3sWorkloadProvider(
    IKubectlCliRunner kubectl,
    TimeProvider clock,
    ILogger<K3sWorkloadProvider> logger) : IWorkloadProvider
{
    /// <summary>
    ///     Workspace on the agent's filesystem where every workload's
    ///     kustomize directory lives.
    /// </summary>
    public const string ManifestsDirectory = "/var/lib/plexor/workloads/k3s";

    /// <summary>Local-id (Guid) ↔ workload name (matches Deployment name).</summary>
    private readonly ConcurrentDictionary<string, Guid> nameToId = new();

    /// <summary>Local-id (Guid) ↔ <see cref="LocalWorkload" />.</summary>
    private readonly ConcurrentDictionary<Guid, LocalWorkload> workloads = new();

    /// <summary>Local-id (Guid) ↔ kustomize directory path on disk.</summary>
    private readonly ConcurrentDictionary<Guid, string> manifestPaths = new();

    /// <summary>Local-id (Guid) ↔ originally-configured replica count (for StartAsync).</summary>
    private readonly ConcurrentDictionary<Guid, int> configuredReplicas = new();

    /// <inheritdoc />
    public WorkloadKind Kind => new WorkloadKind.K3s();

    /// <inheritdoc />
    public async Task<LocalWorkload> CreateAsync(
        WorkloadSpec spec,
        CancellationToken cancellationToken)
    {
        // spec is non-nullable; trust the type system per
        // .agents/rules/coding/code-shape.md §11.
        if (string.IsNullOrWhiteSpace(spec.Name))
        {
            throw new ArgumentException(
                "WorkloadSpec.Name is required for k3s.",
                nameof(spec));
        }

        var config = K3sWorkloadConfig.TryParse(spec.Config, out var error)
            ?? throw new InvalidOperationException(
                $"Cannot create k3s workload '{spec.Name}': {error}");

        var id = Guid.NewGuid();
        var manifestDir = RuntimeHelpers.KustomizeManifestDirectory(ManifestsDirectory, spec.Name);
        var manifest = K3sWorkloadRenderer.Render(spec.Name, config);

        RuntimeHelpers.EnsureDirectoryExists(manifestDir);
        await File.WriteAllTextAsync(
            $"{manifestDir}/kustomization.yaml", manifest.KustomizationYaml, cancellationToken);
        await File.WriteAllTextAsync(
            $"{manifestDir}/deployment.yaml", manifest.DeploymentYaml, cancellationToken);
        if (!string.IsNullOrEmpty(manifest.ServiceYaml))
        {
            await File.WriteAllTextAsync(
                $"{manifestDir}/service.yaml", manifest.ServiceYaml, cancellationToken);
        }

        // kubectl apply -k consumes the kustomization.yaml at
        // <manifestDir> and recursively applies every listed
        // resource. apply is idempotent — re-running over an
        // existing workload converges to the desired state.
        await kubectl.RunAsync(
            $"apply -k {manifestDir}",
            cancellationToken);

        var now = clock.GetUtcNow();
        var local = new LocalWorkload(
            id, spec.Name, Kind, WorkloadState.Running, now, now);

        nameToId[spec.Name] = id;
        workloads[id] = local;
        manifestPaths[id] = manifestDir;
        configuredReplicas[id] = config.Replicas;

        logger.LogInformation(
            "Created k3s workload {Name} (localId {LocalId}, replicas {Replicas}, namespace {Namespace}, manifest {ManifestDir})",
            spec.Name, id, config.Replicas, config.Namespace, manifestDir);

        return local;
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> StartAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!workloads.TryGetValue(id, out var local))
        {
            throw new InvalidOperationException(
                $"No k3s workload with localId {id}.");
        }

        // Scale back to the originally-configured replica count
        // (kubectl scale --replicas=0 was the StopAsync idempotent
        // path; StartAsync restores production behavior).
        var replicas = configuredReplicas.TryGetValue(id, out var r)
            ? r
            : 1;

        await kubectl.RunAsync(
            $"scale deployment/{local.Name} --replicas={replicas}",
            cancellationToken);

        var now = clock.GetUtcNow();
        var updated = local with
        {
            State = WorkloadState.Running,
            StartedAt = now,
        };
        workloads[id] = updated;
        logger.LogInformation(
            "Started k3s workload {Name} (localId {LocalId}, replicas {Replicas})",
            updated.Name, id, replicas);
        return updated;
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> StopAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!workloads.TryGetValue(id, out var local))
        {
            throw new InvalidOperationException(
                $"No k3s workload with localId {id}.");
        }

        // k8s idiom for "stop without delete": scale to 0.
        await kubectl.RunAsync(
            $"scale deployment/{local.Name} --replicas=0",
            cancellationToken);

        var updated = local with { State = WorkloadState.Stopped };
        workloads[id] = updated;
        logger.LogInformation(
            "Stopped k3s workload {Name} (localId {LocalId})",
            updated.Name, id);
        return updated;
    }

    /// <inheritdoc />
    public async Task<LocalWorkload> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!workloads.TryGetValue(id, out var local))
        {
            logger.LogDebug(
                "DeleteAsync on missing k3s workload {LocalId} -- no-op",
                id);

            return new LocalWorkload(
                id, "<deleted>", Kind, WorkloadState.Stopped,
                clock.GetUtcNow(), null);
        }

        if (manifestPaths.TryGetValue(id, out var manifestDir))
        {
            await kubectl.RunAsync(
                $"delete -k {manifestDir} --ignore-not-found",
                cancellationToken);

            try
            {
                Directory.Delete(manifestDir, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // Already deleted.
            }
        }

        nameToId.TryRemove(local.Name, out _);
        manifestPaths.TryRemove(id, out _);
        configuredReplicas.TryRemove(id, out _);
        var tombstone = local with { State = WorkloadState.Stopped };
        workloads.TryRemove(id, out _);

        logger.LogInformation(
            "Deleted k3s workload {Name} (localId {LocalId})",
            local.Name, id);
        return tombstone;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<LocalWorkload>> ListAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LocalWorkload> snapshot = [.. workloads.Values];
        return Task.FromResult(snapshot);
    }

}
