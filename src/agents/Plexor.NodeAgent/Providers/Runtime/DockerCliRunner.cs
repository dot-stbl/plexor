// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DockerCliRunner — IDockerCliRunner implementation that shells out
// to the host's docker CLI. Logs each invocation (args + exit
// code) so operators can trace what the agent did. Process is
// killed on cancellation token (Process.Kill propagates SIGKILL
// on Linux; on Windows it triggers TerminateProcess).
// ============================================================================

using System.Diagnostics;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Default <see cref="IDockerCliRunner" /> implementation. Shells
///     out to <c>docker</c> via <see cref="Process" />. Each
///     invocation runs with the agent's service-account credentials
///     (no privilege escalation — assume the agent runs as a user
///     who can reach the docker socket).
/// </summary>
/// <param name="logger">Structured logger for invocation traces.</param>
public sealed class DockerCliRunner(ILogger<DockerCliRunner> logger) : IDockerCliRunner
{
    /// <inheritdoc />
    public async Task<string> RunAsync(string args, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "docker",
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
            "docker {Args} -> exit {ExitCode}, {StdoutLen} stdout bytes, {StderrLen} stderr bytes",
            args, process.ExitCode, stdout.Length, stderr.Length);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker {args} exited {process.ExitCode}: {stderr.Trim()}");
        }

        return stdout.Trim();
    }
}
