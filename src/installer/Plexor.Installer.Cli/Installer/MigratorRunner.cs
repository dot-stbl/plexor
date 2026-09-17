// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// MigratorRunner — invokes the Plexor.Migrator binary as a
// subprocess to apply pending EF Core migrations after a binary
// swap. Used by `plx upgrade` so the new host binary boots against
// the latest schema.
//
// The migrator binary is expected at <data>/bin/plexor-migrator
// (sibling of plexor-host). If absent, the upgrade continues
// without migrations — the operator can run `plexor-migrator`
// manually before the next host restart. Migration failure aborts
// the upgrade so the binary can be rolled back.
// ============================================================================

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Plexor.Installer.Cli.Installer;

/// <summary>
///     Outcome of running the migrator binary. <see cref="ExitCode" />
///     mirrors the subprocess exit code; <see cref="Skipped" /> is
///     true when the migrator binary is not on disk (operator
///     action required).
/// </summary>
/// <param name="ExitCode">Subprocess exit code (0 = success).</param>
/// <param name="Skipped">True when the binary was absent and no run happened.</param>
/// <param name="Error">Captured stderr on failure.</param>
public sealed record MigratorOutcome(int ExitCode, bool Skipped, string? Error);

/// <summary>
///     Resolve the migrator binary path and run it as a
///     subprocess. Returns <see cref="MigratorOutcome.Skipped" />
///     when the binary is absent (operator can run manually).
/// </summary>
public static class MigratorRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Default migrator binary filename (no extension on Linux).</summary>
    public const string MigratorBinaryName = "plexor-migrator";

    /// <summary>
    ///     Run the migrator binary if present. Non-Linux hosts
    ///     throw <see cref="PlatformNotSupportedException" /> —
    ///     the installer commands are Linux-only in v0.1.
    /// </summary>
    /// <param name="binDirectory">Directory containing <c>plexor-migrator</c>.</param>
    public static MigratorOutcome Run(string binDirectory)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "plx installer commands require Linux");
        }

        var migratorPath = Path.Combine(binDirectory, MigratorBinaryName);
        if (!File.Exists(migratorPath))
        {
            return new MigratorOutcome(0, Skipped: true, Error: null);
        }

        var psi = new ProcessStartInfo
        {
            FileName = migratorPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(DefaultTimeout);

        return new MigratorOutcome(
            process.ExitCode,
            Skipped: false,
            Error: process.ExitCode == 0 ? null : stderr.Trim());
    }
}
