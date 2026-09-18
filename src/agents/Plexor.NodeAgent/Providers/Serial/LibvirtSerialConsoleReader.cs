// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtSerialConsoleReader — file-static helper that spawns
// `virsh console <domain>` and streams the resulting stdout
// line by line. The agent's serial-console read-path.
//
// Why shell out to virsh (vs a LibvirtClient binding):
//   - Same rationale as LibvirtRunner (Sprint 3): v0.1 shells
//     out to the CLI; v0.2+ swaps in LibvirtClient once the
//     API surface stabilises. Replacing the helper then is
//     a one-file change.
//   - virsh console uses libvirt's character-device stream
//     internally, so it Just Works regardless of whether the
//     serial device is a pty (qemu:///system default) or a
//     unix socket / tcp (forwarded-console setups).
//
// How `virsh console` works at the process level:
//   1. Connects to the libvirt daemon over the URI.
//   2. Opens the domain's primary serial console (target_type
//      = serial, port 0 — what LibvirtKvmXmlBuilder writes).
//   3. Reads bytes from the console stream, dumps them to its
//      own stdout. Newlines are preserved.
//   4. The process exits when the console closes (VM stops,
//      guest OS reboots and re-opens the serial, or operator
//      ^]quits from a real terminal).
//
// Cancellation contract:
//   - The caller passes a CancellationToken. We wire it to the
//     Process.Exited wait, so cancelling kills the virsh
//     process. The async sequence completes (with the partial
//     line dropped if a kill happens mid-line).
// ==========================================================================

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Plexor.NodeAgent.Providers.Common;
using Plexor.Shared.Workloads;

namespace Plexor.NodeAgent.Providers.Serial;

/// <summary>
///     Pure-function helper that streams <c>virsh console</c>
///     stdout. Lives in its own file per class-decomposition
///     (helpers without DI → file-static class, not private
///     method on the provider).
/// </summary>
public static class LibvirtSerialConsoleReader
{
    /// <summary>
    ///     Run <c>virsh -c &lt;uri&gt; console &lt;domain&gt;</c> and
    ///     yield each stdout line as the process emits it. The
    ///     sequence ends when:
    ///     <list type="bullet">
    ///         <item>virsh exits (VM stopped, console disconnected).</item>
    ///         <item>The cancellation token fires — we kill the process.</item>
    ///     </list>
    /// </summary>
    /// <param name="uri">Libvirt URI (e.g. <c>qemu:///system</c>).</param>
    /// <param name="domainName">Libvirt domain name to attach to.</param>
    /// <param name="cancellationToken">
    ///     Cancellation token. Cancelling kills the virsh
    ///     process and ends the sequence.
    /// </param>
    public static async IAsyncEnumerable<string> StreamAsync(
        Uri uri,
        string domainName,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName);

        var psi = new ProcessStartInfo
        {
            FileName = "virsh",
            ArgumentList = { "-c", uri.ToString(), "console", domainName },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"LibvirtSerialConsoleReader: Process.Start returned false for virsh console {domainName}.");
        }

        // Drain stderr concurrently so virsh's "Connected to domain …"
        // message doesn't block the reader. We don't surface stderr
        // to the caller — it's mostly cosmetic — but a stall on
        // stderr would block the whole sequence.
        _ = Task.Run(async () =>
        {
            try
            {
                while (await process.StandardError.ReadLineAsync(cancellationToken) is not null)
                {
                    // discard; nothing useful to forward
                }
            }
            catch (IOException)
            {
                // process closed its stderr — fine
            }
        }, cancellationToken);

        // Yield stdout lines until the process exits or the
        // caller cancels. We intentionally don't hold a lock
        // around the ReadLineAsync — async streams run the
        // cancellation handling in the same state machine.
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    // EOF — virsh exited cleanly.
                    break;
                }

                yield return line;
            }
        }
        finally
        {
            // Best-effort: ensure virsh is gone when the
            // enumeration ends (cancellation, or process closed
            // its stdout). Process.Dispose() also kills it
            // (since EnableRaisingEvents is on); the explicit
            // Kill is belt-and-braces in case the process is
            // stuck on a TTY read.
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }
        }
    }

    /// <summary>
    ///     Convenience overload that resolves the URI +
    ///     domain name from a <see cref="WorkloadIdMapEntry" />.
    ///     Lives here (not on the entry) because the entry
    ///     doesn't know which provider owns it.
    /// </summary>
    /// <param name="uri">Libvirt URI (e.g. <c>qemu:///system</c>).</param>
    /// <param name="entry">The workload's local-id map entry.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public static IAsyncEnumerable<string> StreamAsync(
        Uri uri,
        WorkloadIdMapEntry entry,
        CancellationToken cancellationToken)
    {
        return StreamAsync(uri, entry.DomainName, cancellationToken);
    }
}
