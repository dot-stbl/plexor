// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QemuImageRunner — thin wrapper around the `qemu-img` CLI. Same
// pattern as LibvirtRunner: shell out, capture stdout/stderr,
// throw on non-zero exit. LocalDirStorage uses it to create
// qcow2 overlays from base images; future RbdStorage uses
// `rbd` instead.
//
// Stateless. One runner per agent (singleton).
// ==========================================================================

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Plexor.NodeAgent.Providers.Storage;

/// <summary>
///     Run <c>qemu-img &lt;subcommand&gt; &lt;args&gt;</c> and return
///     trimmed stdout. Throws <see cref="InvalidOperationException" />
///     on non-zero exit.
/// </summary>
public static class QemuImageRunner
{
    /// <summary>
    ///     Run the given qemu-img args. Returns trimmed stdout;
    ///     throws on non-zero exit.
    /// </summary>
    /// <param name="args">
    ///     qemu-img command + flags (e.g.
    ///     <c>"create -f qcow2 -b /var/lib/.../base.qcow2 -F qcow2 /var/lib/.../overlay.qcow2 20G"</c>).
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task<string> RunAsync(string args, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "qemu-img",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var segment in args.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            startInfo.ArgumentList.Add(segment);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("qemu-img: Process.Start returned null.");

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
                $"qemu-img exited with code {process.ExitCode.ToString(CultureInfo.InvariantCulture)}. "
                + $"stderr: {stderr.ToString().Trim()}");
        }

        return stdout.ToString().Trim();
    }
}
