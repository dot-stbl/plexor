// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DockerComposeWorkloadProvider — IWorkloadProvider implementation
// for single-host multi-container workloads via the docker CLI
// (matches the WorkloadKind.DockerCompose wire variant).
//
// How it works (v0.1):
//   1. CreateAsync parses the WorkloadSpec.Config JSON into a
//      DockerComposeConfig, renders the docker-compose.yaml via
//      the file-static renderer, writes the file to
//      /var/lib/plexor/workloads/<name>/compose.yaml, then runs
//      `docker compose -f <path> up -d`. The new container's local
//      id (Guid) is tracked in-memory keyed by service name.
//   2. StartAsync/StopAsync/DeleteAsync map to
//      `docker compose -f <path> {start|stop|down}` — idempotent
//      for our purposes (start on a started container is a no-op).
//   3. ListAsync at agent boot reconciles the agent's local view
//      against the existing /var/lib/plexor/workloads/ tree on
//      disk (for restart recovery).
//
// State reporting:
//   - State is read from `docker compose ps --format json`.
//   - Successful ps = WorkloadState.Running.
//   - Stopped container = WorkloadState.Stopped.
//   - Missing manifest = no such workload (ListAsync skips it).
//
// Path convention:
//   Plexor is Linux-only; manifest paths use forward-slash strings,
//   not Path.Combine (which produces '\\' on Windows dev boxes).
//   The host's docker CLI accepts forward-slash paths transparently.
// ============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     <see cref="IWorkloadProvider" /> for Docker Compose workloads
///     (matches <see cref="WorkloadKind.DockerCompose" />).
/// </summary>
/// <param name="docker">
///     The host's <c>docker</c> CLI wrapper. Injected so unit tests
///     can substitute a deterministic fake that records invocations
///     without shelling out to the real binary.
/// </param>
/// <param name="clock">
///     Time provider for <see cref="LocalWorkload.CreatedAt" /> /
///     <see cref="LocalWorkload.StartedAt" />. Defaults to
///     <see cref="TimeProvider.System" /> via DI.
/// </param>
/// <param name="logger">Structured logger for create/start/stop/delete traces.</param>
public sealed class DockerComposeWorkloadProvider(
    IDockerCliRunner docker,
    TimeProvider clock,
    ILogger<DockerComposeWorkloadProvider> logger) : IWorkloadProvider
{
    /// <summary>
    ///     Workspace on the agent's filesystem where every workload's
    ///     <c>compose.yaml</c> lives. Hardcoded for v0.1 (single-host
    ///     deployment on Linux). Phase 7+ configurable via
    ///     <c>NodeAgentOptions</c>.
    /// </summary>
    public const string ManifestsDirectory = "/var/lib/plexor/workloads";

    /// <summary>Local-id (Guid) ↔ service name (workload spec name).</summary>
    private readonly ConcurrentDictionary<string, Guid> nameToId = new();

    /// <summary>Local-id (Guid) ↔ <see cref="LocalWorkload" />.</summary>
    private readonly ConcurrentDictionary<Guid, LocalWorkload> workloads = new();

    /// <summary>Local-id (Guid) ↔ manifest path on disk (one per workload).</summary>
    private readonly ConcurrentDictionary<Guid, string> manifestPaths = new();

    /// <inheritdoc />
    public WorkloadKind Kind => new WorkloadKind.DockerCompose();

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
                "WorkloadSpec.Name is required for docker-compose.",
                nameof(spec));
        }

        var config = DockerComposeConfig.TryParse(spec.Config, out var error)
            ?? throw new InvalidOperationException(
                $"Cannot create docker-compose workload '{spec.Name}': {error}");

        var id = Guid.NewGuid();
        var manifestPath = RuntimeHelpers.ComposeManifestPath(ManifestsDirectory, spec.Name);
        var yaml = DockerComposeRenderer.Render(spec.Name, config);

        RuntimeHelpers.EnsureDirectoryExists(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(manifestPath, yaml, cancellationToken);

        // First-time up: pulls the image, starts the service,
        // prints the compose-defined service name on stdout.
        await docker.RunAsync(
            $"compose -f {manifestPath} up -d",
            cancellationToken);

        var now = clock.GetUtcNow();
        var local = new LocalWorkload(
            id, spec.Name, Kind, WorkloadState.Running, now, now);

        nameToId[spec.Name] = id;
        workloads[id] = local;
        manifestPaths[id] = manifestPath;

        logger.LogInformation(
            "Created docker-compose workload {Name} (localId {LocalId}, manifest {ManifestPath})",
            spec.Name, id, manifestPath);

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
                $"No docker-compose workload with localId {id}.");
        }

        if (!manifestPaths.TryGetValue(id, out var manifestPath))
        {
            throw new InvalidOperationException(
                $"Docker-compose workload {id} has no manifest path.");
        }

        await docker.RunAsync(
            $"compose -f {manifestPath} start",
            cancellationToken);

        var now = clock.GetUtcNow();
        var updated = local with
        {
            State = WorkloadState.Running,
            StartedAt = now,
        };
        workloads[id] = updated;
        logger.LogInformation(
            "Started docker-compose workload {Name} (localId {LocalId})",
            updated.Name, id);
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
                $"No docker-compose workload with localId {id}.");
        }

        if (!manifestPaths.TryGetValue(id, out var manifestPath))
        {
            throw new InvalidOperationException(
                $"Docker-compose workload {id} has no manifest path.");
        }

        await docker.RunAsync(
            $"compose -f {manifestPath} stop",
            cancellationToken);

        var updated = local with { State = WorkloadState.Stopped };
        workloads[id] = updated;
        logger.LogInformation(
            "Stopped docker-compose workload {Name} (localId {LocalId})",
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
                "DeleteAsync on missing docker-compose workload {LocalId} -- no-op",
                id);

            return new LocalWorkload(
                id, "<deleted>", Kind, WorkloadState.Stopped,
                clock.GetUtcNow(), null);
        }

        if (manifestPaths.TryGetValue(id, out var manifestPath))
        {
            // `down` removes stopped containers + the default
            // network; -v removes anonymous volumes. We don't
            // attach named volumes in v0.1, so `-v` is moot.
            await docker.RunAsync(
                $"compose -f {manifestPath} down",
                cancellationToken);

            try
            {
                File.Delete(manifestPath);
            }
            catch (FileNotFoundException)
            {
                // Already deleted — idempotent delete is documented
                // in the IWorkloadProvider contract.
            }
            catch (DirectoryNotFoundException)
            {
                // Same.
            }
        }

        nameToId.TryRemove(local.Name, out _);
        manifestPaths.TryRemove(id, out _);
        var tombstone = local with { State = WorkloadState.Stopped };
        workloads.TryRemove(id, out _);

        logger.LogInformation(
            "Deleted docker-compose workload {Name} (localId {LocalId})",
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
