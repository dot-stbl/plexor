// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// KubectlCliRunner — IKubectlCliRunner implementation. Shells out to
// kubectl pointing at the k3s-supplied kubeconfig. Production
// deployments pointing at a non-k3s cluster would override the
// kubeconfig path via NodeAgentOptions (Phase 7+).
// ==========================================================================

using System.Diagnostics;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Default <see cref="IKubectlCliRunner" /> implementation.
///     Wraps <c>kubectl</c> via <see cref="Process" /> and prepends
///     the k3s-supplied kubeconfig on every call so the rest of
///     the provider stays single-purpose (no kubeconfig boilerplate).
/// </summary>
/// <param name="logger">Structured logger for invocation traces.</param>
public sealed class KubectlCliRunner(ILogger<KubectlCliRunner> logger) : IKubectlCliRunner
{
    /// <summary>
    ///     Default k3s kubeconfig path. The k3s server writes
    ///     this file on install with mode 0600 owned by root;
    ///     when running on a node that's not the k3s server, the
    ///     credential is the one issued at <c>plx init</c> time
    ///     via the host's CertificateAuthority.
    /// </summary>
    public const string KubeconfigPath = "/etc/rancher/k3s/k3s.yaml";

    /// <inheritdoc />
    public async Task<string> RunAsync(string args, CancellationToken cancellationToken)
    {
        var fullArgs = $"--kubeconfig {KubeconfigPath} {args}";

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "kubectl",
            Arguments = fullArgs,
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
            "kubectl {Args} -> exit {ExitCode}, {StdoutLen} stdout bytes, {StderrLen} stderr bytes",
            args, process.ExitCode, stdout.Length, stderr.Length);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"kubectl {args} exited {process.ExitCode}: {stderr.Trim()}");
        }

        return stdout.Trim();
    }
}
