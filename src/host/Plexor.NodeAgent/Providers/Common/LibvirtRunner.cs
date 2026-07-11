// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtRunner — thin wrapper around the `virsh` CLI. v0.1 shells
// out to virsh; v0.2+ will swap in the LibvirtClient C# binding
// for richer async + no shell-quoting footguns.
//
// One runner per libvirt URI. The runner is stateless — it
// captures only the URI; everything else (process start, stdout/
// stderr, exit code) is per-call. Thread-safe by construction:
// the underlying Process.Start is the only shared resource and
// each call creates its own process.
// ============================================================================

using System.Diagnostics;
using System.Text;

namespace Plexor.NodeAgent.Providers.Common;

/// <summary>
/// Run <c>virsh -c &lt;uri&gt; &lt;args&gt;</c> and return trimmed
/// stdout. Throws <see cref="InvalidOperationException"/> on
/// non-zero exit (caller decides whether to swallow, retry, or
/// rethrow).
/// </summary>
public static class LibvirtRunner
{
    /// <summary>Run the given virsh args against the given URI.
    /// Returns trimmed stdout; throws on non-zero exit.</summary>
    /// <param name="uri">Libvirt URI (e.g. <c>qemu:///system</c>,
    /// <c>lxc:///system</c>). The runner prefixes <c>-c</c> and
    /// the URI to the command line so the provider doesn't have
    /// to.</param>
    /// <param name="args">Virsth command + flags (e.g.
    /// <c>"start web-prod-01"</c>, <c>"define /tmp/plexor-x.xml"</c>).
    /// The runner splits on whitespace; arguments with embedded
    /// spaces are not supported (real impl uses LibvirtClient
    /// which doesn't have that problem).</param>
    /// <param name="cancellationToken">Cancellation forwarded to the process.</param>
    public static async Task<string> RunAsync(Uri uri, string args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "virsh",
            ArgumentList = { "-c", uri.ToString() },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException(
                "LibvirtRunner: failed to start virsh (Process.Start returned null).");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"virsh -c {uri} {args} failed (exit {process.ExitCode}): {stderr}".Trim());
        }

        return stdout.ToString().TrimEnd();
    }
}