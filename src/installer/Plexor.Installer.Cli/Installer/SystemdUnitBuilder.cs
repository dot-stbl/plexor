// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SystemdUnitBuilder — generates the systemd unit file content for
// the Plexor.Host service. Pure (no I/O, no DI) — takes settings in,
// returns a string.
//
// The unit template:
//   - Type=simple (foreground process; systemd supervises the PID)
//   - User=root  (privileged ports + system-level mounts)
//   - Restart=on-failure with 5s delay
//   - WorkingDirectory=/var/lib/plexor
//   - StandardOutput=journal + StandardError=journal
//
// To customize, callers prepend their own [Unit] / [Service] / [Install]
// extensions after Build(); the template stays the canonical baseline.
// ============================================================================

using System.Text;

namespace Plexor.Installer.Cli.Installer;

/// <summary>
///     Build the canonical systemd unit file for the Plexor.Host
///     service. Pure: takes paths + description, returns the unit
///     text. No I/O.
/// </summary>
public static class SystemdUnitBuilder
{
    /// <summary>
    ///     Render the unit file content. <paramref name="binaryPath" />
    ///     must be the absolute path to the Plexor.Host binary on
    ///     disk; <paramref name="dataDir" /> must be the host's
    ///     data directory (becomes WorkingDirectory + the PLEXOR_DATA
    ///     hint).
    /// </summary>
    /// <param name="description"></param>
    /// <param name="binaryPath"></param>
    /// <param name="dataDir"></param>
    public static string Build(string description, string binaryPath, string dataDir)
    {
        var sb = new StringBuilder(512);
        sb.AppendLine("[Unit]");
        sb.AppendLine($"Description={description}");
        sb.AppendLine("After=network-online.target");
        sb.AppendLine("Wants=network-online.target");
        sb.AppendLine();
        sb.AppendLine("[Service]");
        sb.AppendLine("Type=simple");
        sb.AppendLine("User=root");
        sb.AppendLine($"WorkingDirectory={dataDir}");
        sb.AppendLine($"ExecStart={binaryPath} run");
        sb.AppendLine("Environment=PLEXOR_DATA_PATH=" + dataDir);
        sb.AppendLine("Environment=ASPNETCORE_URL=http://0.0.0.0:5000");
        sb.AppendLine("Restart=on-failure");
        sb.AppendLine("RestartSec=5");
        sb.AppendLine("StandardOutput=journal");
        sb.AppendLine("StandardError=journal");
        sb.AppendLine();
        sb.AppendLine("[Install]");
        sb.AppendLine("WantedBy=multi-user.target");
        return sb.ToString();
    }
}
