// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpgradeCommand — `plx upgrade`. Atomic in-place upgrade of the
// Plexor.Host binary with rollback on health-check failure.
//
//   1. fetch (URL or local path) the new binary to a staging path,
//   2. stop the running service (if installed and not --no-restart),
//   3. atomic-swap the new binary into <data>/bin/plexor-host,
//   4. run pending EF migrations via plexor-migrator (if present),
//   5. start the service again,
//   6. validate health (GET <health-endpoint>),
//   7. if health fails, roll the binary back and restart.
//
// v0.1 simplification: download happens locally (no auth, no
// checksum). v0.2 adds --checksum <SHA256> and signed URLs.
// ============================================================================

using Plexor.Installer.Cli.Installer;
using Plexor.Installer.Cli.Settings;
using Plexor.Shared.Console;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Plexor.Installer.Commands;

/// <summary>
///     <c>plx upgrade</c> — atomic in-place upgrade of the host
///     binary with rollback on health-check failure. Accepts a
///     local path or an HTTPS URL as the new binary source;
///     performs the swap via
///     <see cref="HostLifecycle.AtomicSwap" />; rolls back via
///     <see cref="HostLifecycle.RollbackSwap" />.
/// </summary>
public sealed class UpgradeCommand : AsyncCommand<UpgradeSettings>
{
    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandContext context, UpgradeSettings settings)
    {
        var validation = InstallerPaths.Validate();
        if (!validation.IsValid)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(validation.Error ?? "path layout invalid"));
            return 2;
        }

        var unitPath = InstallerPaths.UnitFilePath;
        if (!HostLifecycle.IsInstalled(unitPath))
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "not installed",
                $"unit file missing at {unitPath}; run `plx init` first"));
            return 3;
        }

        var currentBinary = Path.Combine(InstallerPaths.BinDirectory, "plexor-host");
        if (!File.Exists(currentBinary))
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                "binary missing",
                $"host binary not found at {currentBinary}"));
            return 4;
        }

        var stagingPath = Path.Combine(InstallerPaths.BinDirectory, "plexor-host.staged");
        var healthEndpoint = InitCommand.DefaultHealthEndpoint;

        try
        {
            await ProgressRunner.RunAsync(async ctx =>
            {
                await InstallerSteps.RunAsync(ctx, "fetching new binary", async () =>
                {
                    var fetch = await BinaryFetcher.FetchAsync(settings.Source, stagingPath);
                    if (fetch.ExitCode != 0)
                    {
                        AnsiConsole.Console.MarkupLine(ErrorFormatter.Error("fetch failed", fetch.Detail));
                    }
                    return fetch.ExitCode;
                });

                if (!settings.NoRestart)
                {
                    await InstallerSteps.RunAsync(ctx, "stopping plexor-host.service", () =>
                        Task.FromResult(HostLifecycle.SystemCtl("stop", "plexor-host.service").ExitCode));
                }

                await InstallerSteps.RunAsync(ctx, "atomic swap", () =>
                {
                    HostLifecycle.AtomicSwap(currentBinary, stagingPath);
                    return Task.FromResult(0);
                });

                await InstallerSteps.RunAsync(ctx, "applying migrations", () =>
                {
                    var outcome = MigratorRunner.Run(InstallerPaths.BinDirectory);
                    if (outcome.Skipped)
                    {
                        AnsiConsole.Console.MarkupLine(ErrorFormatter.Info(
                            "migrator not on disk",
                            $"no {MigratorRunner.MigratorBinaryName} in {InstallerPaths.BinDirectory}; skipping migrations"));
                    }
                    else if (outcome.ExitCode != 0)
                    {
                        AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                            "migrator failed", outcome.Error));
                    }
                    return Task.FromResult(outcome.ExitCode);
                });

                if (!settings.NoRestart)
                {
                    await InstallerSteps.RunAsync(ctx, "starting plexor-host.service", () =>
                        Task.FromResult(HostLifecycle.SystemCtl("start", "plexor-host.service").ExitCode));

                    await InstallerSteps.RunAsync(ctx, "validating health", async () =>
                    {
                        var exit = await HealthProbe.CheckAsync(healthEndpoint);
                        if (exit != HealthProbe.ExitOk)
                        {
                            throw new InvalidOperationException(
                                $"health check failed (exit {exit})");
                        }
                        return 0;
                    });
                }
            });
        }
        catch (Exception ex) when (!settings.NoRestart && ex is InvalidOperationException)
        {
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Error("upgrade failed", ex.Message));
            AnsiConsole.Console.MarkupLine(ErrorFormatter.Warn("rolling back binary"));
            HostLifecycle.RollbackSwap(currentBinary);
            var restart = HostLifecycle.SystemCtl("start", "plexor-host.service");
            if (restart.ExitCode != 0)
            {
                AnsiConsole.Console.MarkupLine(ErrorFormatter.Error(
                    "rollback restart failed", restart.Error));
                return 8;
            }
            return 7;
        }

        AnsiConsole.Console.MarkupLine(MarkupExtensions.Ok("upgrade complete"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted($"  binary:    {currentBinary}"));
        AnsiConsole.Console.MarkupLine(MarkupExtensions.Muted($"  previous:  {currentBinary}.prev"));
        return 0;
    }
}
