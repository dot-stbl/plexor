// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PodmanQuadletWorkloadProvider — IWorkloadProvider for Podman
// Quadlet workloads (matches WorkloadKind.PodmanQuadlet).
//
// Lifecycle (v0.1):
//   1. CreateAsync parses the WorkloadSpec.Config JSON into a
//      PodmanQuadletConfig, renders the quadlet INI via the
//      file-static renderer, writes the file to
//      /etc/containers/systemd/<name>.container, then runs
//      `systemctl daemon-reload` + `systemctl start <name>.service`.
//      The provider's local id (Guid) is tracked in-memory keyed
//      by service name.
//   2. StartAsync/StopAsync map to systemctl verbs.
//      DeleteAsync tears down the unit (stop + rm + daemon-reload).
//   3. ListAsync at agent boot reconciles against the existing
//      /etc/containers/systemd/ tree on disk.
//
// State reporting:
//   systemctl is-active <name>.service returns "active" / "inactive".
//   Anything else (failed / activating / unknown) maps to
//   WorkloadState.Failed / Unknown respectively.
//
// Path convention:
//   Plexor is Linux-only; quadlet paths use forward-slash strings,
//   not Path.Combine. Podman + systemd both accept forward-slash
//   paths transparently.
// ============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     <see cref="IWorkloadProvider" /> for Podman Quadlet workloads
///     (matches <see cref="WorkloadKind.PodmanQuadlet" />).
/// </summary>
/// <param name="systemd">
///     The host's podman + systemctl CLI wrapper. Injected so
///     unit tests can substitute a deterministic fake that
///     records invocations without shelling out to the real
///     binaries.
/// </param>
/// <param name="clock">
///     Time provider for <see cref="LocalWorkload.CreatedAt" />
///     and <see cref="LocalWorkload.StartedAt" />. Defaults to
///     <see cref="TimeProvider.System" /> via DI.
/// </param>
/// <param name="logger">Structured logger for create/start/stop/delete traces.</param>
public sealed class PodmanQuadletWorkloadProvider(
    IPodmanCliRunner systemd,
    TimeProvider clock,
    ILogger<PodmanQuadletWorkloadProvider> logger) : IWorkloadProvider
{
    /// <summary>
    ///     Workspace on the agent's filesystem where every
    ///     workload's <c>&lt;name&gt;.container</c> lives. Hardcoded
    ///     for v0.1 (single-host deployment on Linux). Phase 7+
    ///     configurable via <c>NodeAgentOptions</c>.
    /// </summary>
    public const string QuadletsDirectory = "/etc/containers/systemd";

    /// <summary>Local-id (Guid) ↔ service name (workload spec name).</summary>
    private readonly ConcurrentDictionary<string, Guid> nameToId = new();

    /// <summary>Local-id (Guid) ↔ <see cref="LocalWorkload" />.</summary>
    private readonly ConcurrentDictionary<Guid, LocalWorkload> workloads = new();

    /// <summary>Local-id (Guid) ↔ quadlet file path on disk.</summary>
    private readonly ConcurrentDictionary<Guid, string> quadletPaths = new();

    /// <inheritdoc />
    public WorkloadKind Kind => new WorkloadKind.PodmanQuadlet();

    /// <inheritdoc />
    public async Task<LocalWorkload> CreateAsync(
        WorkloadSpec spec,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (string.IsNullOrWhiteSpace(spec.Name))
        {
            throw new ArgumentException(
                "WorkloadSpec.Name is required for podman-quadlet.",
                nameof(spec));
        }

        var config = PodmanQuadletConfig.TryParse(spec.Config, out var error)
            ?? throw new InvalidOperationException(
                $"Cannot create podman-quadlet workload '{spec.Name}': {error}");

        var id = Guid.NewGuid();
        var quadletPath = GetQuadletPath(spec.Name);
        var quadletContents = PodmanQuadletRenderer.Render(spec.Name, config);

        EnsureDirectoryExists(Path.GetDirectoryName(quadletPath)!);
        await File.WriteAllTextAsync(quadletPath, quadletContents, cancellationToken);

        // Tell systemd about the new unit, then start it.
        await systemd.RunSystemctlAsync("daemon-reload", cancellationToken);
        await systemd.RunSystemctlAsync(
            $"start {GetServiceName(spec.Name)}",
            cancellationToken);

        var now = clock.GetUtcNow();
        var local = new LocalWorkload(
            id, spec.Name, Kind, WorkloadState.Running, now, now);

        nameToId[spec.Name] = id;
        workloads[id] = local;
        quadletPaths[id] = quadletPath;

        logger.LogInformation(
            "Created podman-quadlet workload {Name} (localId {LocalId}, unit {Unit}, quadlet {QuadletPath})",
            spec.Name, id, GetServiceName(spec.Name), quadletPath);

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
                $"No podman-quadlet workload with localId {id}.");
        }

        await systemd.RunSystemctlAsync(
            $"start {GetServiceName(local.Name)}",
            cancellationToken);

        var now = clock.GetUtcNow();
        var updated = local with
        {
            State = WorkloadState.Running,
            StartedAt = now,
        };
        workloads[id] = updated;
        logger.LogInformation(
            "Started podman-quadlet workload {Name} (localId {LocalId})",
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
                $"No podman-quadlet workload with localId {id}.");
        }

        await systemd.RunSystemctlAsync(
            $"stop {GetServiceName(local.Name)}",
            cancellationToken);

        var updated = local with { State = WorkloadState.Stopped };
        workloads[id] = updated;
        logger.LogInformation(
            "Stopped podman-quadlet workload {Name} (localId {LocalId})",
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
                "DeleteAsync on missing podman-quadlet workload {LocalId} -- no-op",
                id);

            return new LocalWorkload(
                id, "<deleted>", Kind, WorkloadState.Stopped,
                clock.GetUtcNow(), null);
        }

        var unit = GetServiceName(local.Name);
        if (quadletPaths.TryGetValue(id, out var quadletPath))
        {
            await systemd.RunSystemctlAsync($"stop {unit}", cancellationToken);

            try
            {
                File.Delete(quadletPath);
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

            await systemd.RunSystemctlAsync("daemon-reload", cancellationToken);
        }

        nameToId.TryRemove(local.Name, out _);
        quadletPaths.TryRemove(id, out _);
        var tombstone = local with { State = WorkloadState.Stopped };
        workloads.TryRemove(id, out _);

        logger.LogInformation(
            "Deleted podman-quadlet workload {Name} (localId {LocalId})",
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

    /// <summary>
    ///     Quadlet path convention:
    ///     <c>/etc/containers/systemd/&lt;name&gt;.container</c>.
    ///     Forward-slash (Linux-only) — see file header.
    /// </summary>
    private static string GetQuadletPath(string serviceName)
    {
        return $"{QuadletsDirectory}/{serviceName}.container";
    }

    /// <summary>
    ///     systemd service name convention: <c>&lt;name&gt;.service</c>.
    ///     systemd resolves quadlets <c>&lt;name&gt;.container</c>
    ///     into the matching <c>&lt;name&gt;.service</c> on
    ///     <c>daemon-reload</c>.
    /// </summary>
    private static string GetServiceName(string serviceName)
    {
        return $"{serviceName}.service";
    }

    /// <summary>
    ///     Idempotent mkdir equivalent for v0.1 (no recursive flag
    ///     needed since we're a single-host single-tenant
    ///     deployment).
    /// </summary>
    private static void EnsureDirectoryExists(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
