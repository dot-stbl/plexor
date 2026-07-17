// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IPodmanCliRunner — abstracts podman / systemctl shell-out. Parallel
// to IDockerCliRunner; same shape, different command binary.
//
// Podman Quadlet deployments touch TWO different CLIs:
//   - podman (for image pulls, listing containers)
//   - systemctl (for the quadlet unit: daemon-reload + start/stop)
//
// Two methods on one interface — explicit binary selection beats
// a magic-prefix heuristic at the call site. v0.1 implementations
// share a Process-wrapper internally; split into separate runners
// in v0.2+ once we add systemd-nspawn support on top.
// ============================================================================

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Shell-out to either <c>podman</c> or <c>systemctl</c> on the
///     host. Captures stdout, throws on non-zero exit. Two methods
///     so call sites pick the binary explicitly — no magic prefix
///     inference.
/// </summary>
public interface IPodmanCliRunner
{
    /// <summary>
    ///     Run <c>podman &lt;args&gt;</c> synchronously. Returns
    ///     the captured stdout (trimmed). Throws
    ///     <see cref="InvalidOperationException" /> with the
    ///     captured stderr when <c>podman</c> exits non-zero.
    /// </summary>
    /// <param name="args">Arguments passed to the podman binary.</param>
    /// <param name="cancellationToken"></param>
    public Task<string> RunPodmanAsync(string args, CancellationToken cancellationToken);

    /// <summary>
    ///     Run <c>systemctl &lt;args&gt;</c> synchronously. Returns
    ///     the captured stdout (trimmed). Throws
    ///     <see cref="InvalidOperationException" /> with the
    ///     captured stderr when <c>systemctl</c> exits non-zero.
    /// </summary>
    /// <param name="args">Arguments passed to the systemctl binary.</param>
    /// <param name="cancellationToken"></param>
    public Task<string> RunSystemctlAsync(string args, CancellationToken cancellationToken);
}
