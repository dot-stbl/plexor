// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InitCommand — `plx init`. Bootstrap a Plexor cluster on this host by:
//   1. validating the on-host path layout (InstallerPaths.Validate),
//   2. preparing directories (data + bin + config + mtls),
//   3. installing the host binary to <data>/bin/plexor-host,
//   4. bootstrapping the local CA + host server cert (mTLS plane),
//   5. writing the systemd unit file,
//   6. enabling + starting the service (unless --no-start).
//
// Failure modes:
//   - Non-Linux host → PlatformNotSupportedException (caught by builder).
//   - Existing install + no --purge → non-zero exit with a clear message.
//   - Validation failure → non-zero exit with the path validation reason.
//
// All filesystem + systemctl + CA bootstrap work happens via helpers
// in Installer/; the command orchestrates and renders progress.
// The final summary table is rendered by InitSummaryPrinter (file-
// static helper, per code-shape §1a).
// ============================================================================

using Plexor.Installer.Cli.Installer;
using Plexor.Installer.Cli.Settings;
using Plexor.Shared.Console;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Plexor.Installer.Commands;

/// <summary>
///     <c>plx init</c> — bootstrap a Plexor cluster on this host.
///     Validates paths, prepares directories, copies the host
///     binary, bootstraps the CA, writes the systemd unit, and
///     (unless <c>--no-start</c>) enables + starts the service.
///     Prints a summary table with the host URL, CA path, and
///     next-step suggestions at the end.
/// </summary>
public sealed class InitCommand : AsyncCommand<InitSettings>
{
    /// <summary>The default host URL written into the systemd unit and surfaced in the summary.</summary>
    public const string DefaultHostUrl = "http://0.0.0.0:5000";

    /// <summary>The default health endpoint probed by <c>plx upgrade</c>.</summary>
    public const string DefaultHealthEndpoint = "http://localhost:5000/health";

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, InitSettings settings)
    {
        var validation = InstallerPaths.Validate();
        if (!validation.IsValid)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(validation.Error ?? "path layout invalid"));
            return 2;
        }

        var unitPath = InstallerPaths.UnitFilePath;
        if (HostLifecycle.IsInstalled(unitPath) && !settings.Purge)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "already installed",
                $"unit file exists at {unitPath}; pass --purge to overwrite"));
            return 3;
        }

        var sourceBinary = InstallerPaths.CurrentExecutablePath;
        if (string.IsNullOrEmpty(sourceBinary) || !File.Exists(sourceBinary))
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "source binary not found",
                $"could not resolve the running binary path (got '{sourceBinary}')"));
            return 4;
        }

        var clusterName = settings.Name ?? Environment.MachineName;
        var region = settings.Region ?? "default";
        var dataDir = InstallerPaths.DataDirectory;
        var binDir = InstallerPaths.BinDirectory;
        var configDir = InstallerPaths.ConfigDirectory;
        var mtlsDir = Path.Combine(dataDir, "mtls");
        var targetBinary = Path.Combine(binDir, "plexor-host");
        var description = $"Plexor.Host (cluster {clusterName}, region {region})";

        await ProgressRunner.RunAsync(async ctx =>
        {
            await InstallerSteps.RunAsync(ctx, "preparing directories", () =>
            {
                Directory.CreateDirectory(dataDir);
                Directory.CreateDirectory(binDir);
                Directory.CreateDirectory(configDir);
                Directory.CreateDirectory(mtlsDir);
                return Task.FromResult(0);
            });

            await InstallerSteps.RunAsync(ctx, "installing binary", () =>
            {
                File.Copy(sourceBinary, targetBinary, overwrite: true);
                return Task.FromResult(0);
            });

            await InstallerSteps.RunAsync(ctx, "bootstrapping mTLS CA + host cert", () =>
            {
                PlexorCaBootstrapInvoker.EnsureCertificates(dataDir);
                return Task.FromResult(0);
            });

            await InstallerSteps.RunAsync(ctx, "writing systemd unit", async () =>
            {
                var unit = SystemdUnitBuilder.Build(description, targetBinary, dataDir);
                await File.WriteAllTextAsync(unitPath, unit);
                return 0;
            });

            if (!settings.NoStart)
            {
                await InstallerSteps.RunAsync(ctx, "systemctl daemon-reload", () =>
                    Task.FromResult(HostLifecycle.SystemCtl("daemon-reload").ExitCode));

                await InstallerSteps.RunAsync(ctx, "systemctl enable --now plexor-host.service", () =>
                    Task.FromResult(HostLifecycle.SystemCtl("enable", "--now", "plexor-host.service").ExitCode));
            }
        });

        InitSummaryPrinter.Print(
            clusterName,
            region,
            dataDir,
            targetBinary,
            unitPath,
            mtlsDir,
            settings.NoStart,
            DefaultHostUrl,
            DefaultHealthEndpoint);
        return 0;
    }
}
