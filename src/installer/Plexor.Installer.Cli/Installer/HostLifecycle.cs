// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// HostLifecycle — systemctl + Process.Start wrappers for the
// installer. All methods return a Result-like record (no exceptions
// on expected failure paths like "not installed" or "service not
// running"). NativeAOT-compatible — System.Diagnostics.Process is
// AOT-friendly.
//
// v0.1 scope: Linux-only. Windows returns NotSupported with a friendly
// message. (The CLI runs everywhere; the host service only deploys
// on Linux in v0.1.)
// ============================================================================

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Plexor.Installer.Cli.Installer;

/// <summary>
///     systemctl + Process.Start wrappers for the installer
///     commands. Pure orchestrator — no I/O outside process
///     invocations; no DI.
/// </summary>
public static class HostLifecycle
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Outcome of a process invocation. <see cref="ExitCode" />
    ///     is the captured exit code; <see cref="Output" /> and
    ///     <see cref="Error" /> carry stdout/stderr for diagnostics.
    /// </summary>
    /// <param name="ExitCode"></param>
    /// <param name="Output"></param>
    /// <param name="Error"></param>
    public sealed record ProcessOutcome(int ExitCode, string Output, string Error)
    {
        /// <summary>Process succeeded.</summary>
        public bool Succeeded => ExitCode == 0;
    }

    /// <summary>
    ///     Run an external process, capturing stdout + stderr.
    ///     Returns synchronously after the process exits. Throws
    ///     <see cref="PlatformNotSupportedException" /> when invoked
    ///     from a non-Linux host — installer is Linux-only in v0.1.
    /// </summary>
    public static ProcessOutcome Run(string fileName, IEnumerable<string> arguments)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "plx installer commands require Linux");
        }

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(DefaultTimeout);
        return new ProcessOutcome(process.ExitCode, stdout, stderr);
    }

    /// <summary>
    ///     Convenience wrapper for `systemctl` with the supplied
    ///     subcommand + args. Throws on non-Linux hosts.
    /// </summary>
    /// <param name="systemctlArgs"></param>
    public static ProcessOutcome SystemCtl(params string[] systemctlArgs)
    {
        var args = new List<string> { "--no-pager" };
        args.AddRange(systemctlArgs);
        return Run("systemctl", args);
    }

    /// <summary>
    ///     True when the supplied unit file exists on disk. Pure
    ///     filesystem probe — no DI.
    /// </summary>
    /// <param name="unitFilePath"></param>
    public static bool IsInstalled(string unitFilePath)
    {
        return File.Exists(unitFilePath);
    }

    /// <summary>
    ///     Atomic binary swap. Move current binary to <c>.prev</c>,
    ///     copy new binary in place, leave the old available for
    ///     rollback. Both files must exist after the call.
    /// </summary>
    /// <param name="currentBinary"></param>
    /// <param name="newBinary"></param>
    public static void AtomicSwap(string currentBinary, string newBinary)
    {
        var backup = currentBinary + ".prev";
        if (File.Exists(backup))
        {
            File.Delete(backup);
        }

        if (File.Exists(currentBinary))
        {
            File.Move(currentBinary, backup);
        }

        File.Copy(newBinary, currentBinary);
    }

    /// <summary>
    ///     Inverse of <see cref="AtomicSwap" />. Moves the
    ///     <c>.prev</c> file back over <paramref name="currentBinary" />
    ///     so the previously-running binary is restored on disk.
    ///     No-op if the backup is missing (already rolled back or
    ///     the swap was never performed).
    /// </summary>
    /// <param name="currentBinary">Path the swapped-in binary lives at.</param>
    public static void RollbackSwap(string currentBinary)
    {
        var backup = currentBinary + ".prev";
        if (!File.Exists(backup))
        {
            return;
        }

        if (File.Exists(currentBinary))
        {
            File.Delete(currentBinary);
        }

        File.Move(backup, currentBinary);
    }
}
