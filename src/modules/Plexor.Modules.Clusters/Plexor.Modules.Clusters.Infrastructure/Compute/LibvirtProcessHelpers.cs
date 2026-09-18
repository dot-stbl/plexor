// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtProcessHelpers — pure-function helpers shared by
// LibvirtComputeProvider + VirshProcessRunner. Extracted to a
// separate file per class-layout-and-tooling.md §1a (no private
// methods in production classes); the helpers are stateless and
// have no dependencies, so a single `internal static class` keeps
// them co-located.
//
// What lives here:
//   - SanitizeName         — ASCII-letter-only sanitiser for libvirt
//                            domain names (libvirt rejects spaces,
//                            punctuation, non-ASCII).
//   - ParseVmPowerState    — maps virsh domstate stdout text to the
//                            closed VmPowerState enum.
//   - MapVirshExitCode     — maps virsh numeric exit codes to the
//                            canonical ComputeProviderErrorCodes
//                            (42 = NotFound, anything else = Invalid).
//   - WaitForExitOrKill    — bounded wait on a Process; kills the
//                            process tree on timeout / cancellation.
// ============================================================================

using System.Diagnostics;
using Plexor.Shared.Kernel.Compute;

namespace Plexor.Modules.Clusters.Infrastructure.Compute;

/// <summary>
///     Pure-function helpers for the libvirt provider. No I/O, no
///     state, no DI — call sites instantiate via static method
///     dispatch. The Process-lifecycle helpers
///     (<see cref="WaitForExitOrKillAsync" />) are intentionally
///     stateless and side-effect-free apart from the
///     <see cref="Process" /> argument they wrap; that keeps them
///     testable without spinning up a real libvirt daemon.
/// </summary>
internal static class LibvirtProcessHelpers
{
    /// <summary>
    ///     Sanitise an operator-facing name into a string libvirt
    ///     will accept as a domain name. libvirt's domain-name
    ///     grammar is the union of <c>[A-Za-z0-9_-]</c>; any other
    ///     character (space, dot, slash, non-ASCII) gets replaced
    ///     with an underscore and the result is trimmed.
    /// </summary>
    /// <param name="raw">Operator-facing name (e.g. <c>web-prod-01</c>).</param>
    /// <returns>
    ///     A libvirt-safe name (1–N ASCII letters / digits / dashes
    ///     / underscores). Falls back to <c>"vm-{first 12 hex of
    ///     a new GUID}"</c> when the input is empty / whitespace
    ///     after sanitisation — ensures the provider always returns
    ///     a non-empty name (libvirt rejects the empty string).
    /// </returns>
    internal static string SanitizeName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return $"vm-{Guid.NewGuid():N}"[..12];
        }

        var buffer = raw.Length <= 256 ? stackalloc char[raw.Length] : new char[raw.Length];
        for (var index = 0; index < raw.Length; index++)
        {
            var c = raw[index];
            buffer[index] = char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_';
        }

        var sanitised = new string(buffer).Trim('_');
        return sanitised.Length is 0 ? $"vm-{Guid.NewGuid():N}"[..12] : sanitised;
    }

    /// <summary>
    ///     Parse the stdout of <c>virsh domstate &lt;domain&gt;</c>
    ///     into the closed <see cref="VmPowerState" /> enum.
    /// </summary>
    /// <param name="domstateOutput">
    ///     Trimmed stdout. Recognised values: <c>"running"</c>,
    ///     <c>"shut off"</c>, <c>"shutdown"</c>, <c>"paused"</c>,
    ///     <c>"crashed"</c>, <c>"pmsuspended"</c>. Anything else
    ///     falls through to <see cref="VmPowerState.Unknown" /> so
    ///     a future libvirt version adding a new state doesn't
    ///     blow up the host.
    /// </param>
    /// <returns>The mapped power state.</returns>
    internal static VmPowerState ParseVmPowerState(string domstateOutput)
    {
        return domstateOutput.Trim() switch
        {
            "running" => VmPowerState.Running,
            "shut off" or "shutdown" => VmPowerState.Stopped,
            _ => VmPowerState.Unknown,
        };
    }

    /// <summary>
    ///     Map a virsh exit code to the canonical
    ///     <see cref="ComputeProviderErrorCodes" /> value.
    /// </summary>
    /// <param name="exitCode">
    ///     The process exit code from <c>virsh</c>. Reference:
    ///     <c>0</c> = success, <c>42</c> = "domain not found",
    ///     <c>63</c> = "operation unsupported", anything else is a
    ///     generic libvirt failure.
    /// </param>
    /// <returns>
    ///     <see cref="ComputeProviderErrorCodes.NotFound" /> for the
    ///     "domain not found" code (42);
    ///     <see cref="ComputeProviderErrorCodes.RequestInvalid" />
    ///     for everything else (syntax / usage / duplicate name).
    /// </returns>
    internal static string MapVirshExitCode(int exitCode)
    {
        return exitCode switch
        {
            42 => ComputeProviderErrorCodes.NotFound,
            _ => ComputeProviderErrorCodes.RequestInvalid,
        };
    }

    /// <summary>
    ///     Wait for <paramref name="process" /> to exit, bounded by
    ///     <paramref name="timeout" />. On timeout OR caller-side
    ///     cancellation, kill the process tree (children included)
    ///     and either return <c>false</c> (timeout) or rethrow the
    ///     <see cref="OperationCanceledException" />.
    /// </summary>
    /// <param name="process">The running <see cref="Process" />.</param>
    /// <param name="timeout">Maximum time to wait before forcing a kill.</param>
    /// <param name="cancellationToken">Caller-side cancellation.</param>
    /// <returns>
    ///     <c>true</c> when the process exited cleanly within the
    ///     timeout; <c>false</c> when the timeout fired.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     Rethrown when <paramref name="cancellationToken" /> fires.
    ///     The process tree is killed first.
    /// </exception>
    internal static async Task<bool> WaitForExitOrKillAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        // .NET 10's Process.WaitForExitAsync has no TimeSpan overload;
        // we wire the timeout via a linked CancellationTokenSource and
        // distinguish the two cancel sources in the catch arms.
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);
        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TryKillAsync(process);
            throw;
        }
        catch (OperationCanceledException)
        {
            // Timeout fired (caller token is still live). Kill the
            // process tree so we don't leak a zombie.
            await TryKillAsync(process);
            return false;
        }
    }

    /// <summary>
    ///     Best-effort kill of <paramref name="process" /> and its
    ///     children. Idempotent — calling on an already-exited process
    ///     is a silent no-op (the inner exceptions are swallowed
    ///     because the caller has nothing actionable to do with them:
    ///     either the timeout already fired or the process exited in
    ///     the gap between the wait and the kill).
    /// </summary>
    /// <param name="process">The process to terminate.</param>
    internal static async Task TryKillAsync(Process process)
    {
        try
        {
            // Drain any in-flight stdout/stderr so we don't leave
            // the redirected streams unclosed (which would surface
            // as ObjectDisposedException on the next BeginOutputReadLine).
            await process.WaitForExitAsync();
        }
        catch
        {
            // Already exited or kill raced with exit — fall through to Kill().
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process may have exited between WaitForExitAsync and Kill;
            // not actionable here.
        }
    }
}
