// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PodmanCliRunner — IPodmanCliRunner implementation. Shells out to
// either `podman <args>` or `systemctl <args>` depending on the call
// site (each method explicitly names the binary).
// ============================================================================

using System.Diagnostics;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Default <see cref="IPodmanCliRunner" /> implementation.
///     Same Process-wrapper pattern as <see cref="DockerCliRunner" />;
///     separated because the binary selection is part of the
///     public contract (call sites explicitly name podman vs
///     systemctl). Future v0.2+ may add a third binary
///     (<c>loginctl</c> for user-bus integration) on the same
///     interface.
/// </summary>
/// <param name="logger">Structured logger for invocation traces.</param>
public sealed class PodmanCliRunner(ILogger<PodmanCliRunner> logger) : IPodmanCliRunner
{
    /// <inheritdoc />
    public Task<string> RunPodmanAsync(string args, CancellationToken cancellationToken)
    {
        return RunBinaryAsync("podman", args, cancellationToken);
    }

    /// <inheritdoc />
    public Task<string> RunSystemctlAsync(string args, CancellationToken cancellationToken)
    {
        return RunBinaryAsync("systemctl", args, cancellationToken);
    }

    private async Task<string> RunBinaryAsync(
        string binary, string args, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = binary,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        logger.LogDebug(
            "{Binary} {Args} -> exit {ExitCode}, {StdoutLen} stdout bytes, {StderrLen} stderr bytes",
            binary, args, process.ExitCode, stdout.Length, stderr.Length);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{binary} {args} exited {process.ExitCode}: {stderr.Trim()}");
        }

        return stdout.Trim();
    }
}
