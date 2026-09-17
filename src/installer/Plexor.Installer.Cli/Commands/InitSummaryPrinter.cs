// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InitSummaryPrinter — file-static helper that renders the `plx init`
// summary table (cluster name, paths, status, next steps). Kept
// outside InitCommand per code-shape.md §1a — no private methods on
// production classes; pure presentation logic lives next to its
// caller in a file-static class.
// ============================================================================

using Plexor.Installer.Cli.Installer;
using Plexor.Shared.Console;
using Spectre.Console;

namespace Plexor.Installer.Commands;

/// <summary>
///     Renders the post-init summary table. Pure presentation — no
///     side effects, no DI.
/// </summary>
public static class InitSummaryPrinter
{
    /// <summary>
    ///     Write the success summary table + the three most useful
    ///     next-step hints. Call this once after the install steps
    ///     have all succeeded.
    /// </summary>
    /// <param name="clusterName">Resolved cluster name (settings or hostname).</param>
    /// <param name="region">Resolved region label.</param>
    /// <param name="dataDir">Resolved data directory.</param>
    /// <param name="targetBinary">Path the host binary was copied to.</param>
    /// <param name="unitPath">Path the systemd unit file was written to.</param>
    /// <param name="mtlsDir">Directory containing the CA + host cert.</param>
    /// <param name="noStart">True when <c>--no-start</c> was passed (no systemctl start).</param>
    /// <param name="hostUrl">Host URL written into the unit file.</param>
    /// <param name="healthEndpoint">Health endpoint surfaced for the operator.</param>
    public static void Print(
        string clusterName,
        string region,
        string dataDir,
        string targetBinary,
        string unitPath,
        string mtlsDir,
        bool noStart,
        string hostUrl,
        string healthEndpoint)
    {
        var table = new TableBuilder()
                .WithTitle($"Plexor cluster '{clusterName}' initialised")
                .AddColumn("Key", align: ColumnAlign.Left)
                .AddColumn("Value", align: ColumnAlign.Left)
                .Build();

        table.AddRow("cluster", MarkupExtensions.Accent(clusterName));
        table.AddRow("region", MarkupExtensions.Muted(region));
        table.AddRow("data dir", MarkupExtensions.Muted(dataDir));
        table.AddRow("binary", MarkupExtensions.Muted(targetBinary));
        table.AddRow("unit file", MarkupExtensions.Muted(unitPath));
        table.AddRow("CA cert", MarkupExtensions.Muted(Path.Combine(mtlsDir, "ca.crt")));
        table.AddRow("host cert", MarkupExtensions.Muted(Path.Combine(mtlsDir, "host.crt")));
        table.AddRow("host URL", MarkupExtensions.Accent(hostUrl));
        table.AddRow("health", MarkupExtensions.Muted(healthEndpoint));
        table.AddRow("status", noStart
            ? MarkupExtensions.Warn("not started (--no-start)")
            : MarkupExtensions.Ok("enabled + started"));

        AnsiConsole.Write(table);
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted("Next steps:"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            $"  - join a node:    plx node join --host {hostUrl}"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            "  - check logs:     journalctl -u plexor-host -f"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
            "  - tear down:      plx destroy [--purge]"));
    }
}
