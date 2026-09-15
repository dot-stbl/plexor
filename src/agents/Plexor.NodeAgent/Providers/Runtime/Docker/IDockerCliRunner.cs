// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IDockerCliRunner — abstracts `docker <args>` shell-out so unit tests
// can substitute a deterministic in-memory runner. The agent runs
// docker compose / docker ps on the host's docker CLI directly
// (no docker-in-docker, no remote socket): the same binary that the
// workloads target is what the provider invokes.
//
// Every method captures stdout, throws on non-zero exit, and is
// cancellation-aware (kills the process on cancellation token).
// v0.1 implementation is a thin Process wrapper; v0.2+ swaps in
// the Docker Engine API client once we need richer async
// (multi-event stream, container stats, etc).
// ============================================================================

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Shell-out to the host's docker CLI. Every method captures
///     stdout and throws <see cref="InvalidOperationException" /> on
///     non-zero exit (with stderr in the message for diagnostics).
/// </summary>
public interface IDockerCliRunner
{
    /// <summary>
    ///     Run <c>docker &lt;args&gt;</c> synchronously. Returns the
    ///     captured stdout (trimmed). Throws
    ///     <see cref="InvalidOperationException" /> with the captured
    ///     stderr when <c>docker</c> exits non-zero.
    /// </summary>
    /// <param name="args">Arguments passed to the docker binary.</param>
    /// <param name="cancellationToken"></param>
    public Task<string> RunAsync(string args, CancellationToken cancellationToken);
}
