// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DestroyCommand — `plx destroy`. Tear down the Plexor cluster on
// this host:
//   1. prompt the operator for confirmation (skip with --yes),
//   2. systemctl stop + disable plexor-host.service,
//   3. remove the unit file,
//   4. optionally remove the data directory (--purge).
//
// Audit-friendly: by default the data dir is kept so operators can
// inspect post-mortem. `--purge` deletes it. `--yes` is required
// for non-interactive runs.
// ============================================================================

using Plexor.Installer.Cli.Installer;
using Plexor.Installer.Cli.Settings;
using Plexor.Shared.Console;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Plexor.Installer.Commands;

/// <summary>
///     <c>plx destroy</c> — tear down the host install. Keeps the
///     data directory by default (audit trail); pass <c>--purge</c>
///     to wipe it. Pass <c>--yes</c> to skip the confirmation
///     prompt (CI / scripted).
/// </summary>
public sealed class DestroyCommand : Command<DestroySettings>
{
    /// <inheritdoc />
    public override int Execute(CommandContext context, DestroySettings settings)
    {
        var validation = InstallerPaths.Validate();
        if (!validation.IsValid)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(validation.Error ?? "path layout invalid"));
            return 2;
        }

        var unitPath = InstallerPaths.UnitFilePath;
        var installed = HostLifecycle.IsInstalled(unitPath);

        if (!installed && !settings.Yes)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Info(
                "nothing to destroy",
                $"no unit file at {unitPath}; pass --yes to also delete the data directory"));
            return 0;
        }

        if (!settings.Yes)
        {
            var confirm = AnsiConsole.Console.Prompt(
                new ConfirmationPrompt(
                    MarkupExtensions.Warn(
                        $"destroy Plexor cluster on this host ({(settings.Purge ? "PURGE data" : "keep data")})?")
                    + " [y/N]"));
            if (!confirm)
            {
                AnsiConsole.Console.MarkupLine(ErrorFormatter.Info("cancelled", "no changes made"));
                return 0;
            }
        }

        if (installed)
        {
            var stop = HostLifecycle.SystemCtl("disable", "--now", "plexor-host.service");
            if (stop.ExitCode != 0)
            {
                AnsiConsole.Console.MarkupLine(ErrorFormatter.Warn(
                    "systemctl disable failed", stop.Error));
            }

            File.Delete(unitPath);
        }

        if (settings.Purge && Directory.Exists(InstallerPaths.DataDirectory))
        {
            Directory.Delete(InstallerPaths.DataDirectory, recursive: true);
        }

        AnsiConsole.Console.MarkupLine(MarkupExtensions.Ok("destroyed"));
        if (!settings.Purge)
        {
            AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted(
                $"  data kept at {InstallerPaths.DataDirectory} (use --purge to remove)"));
        }

        return 0;
    }
}
