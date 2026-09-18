// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VirshProcessRunner — concrete IVirshProcessRunner that shells out
// to the `virsh` CLI. v0.1 implementation; v0.2+ swaps the Process
// shell-out for a direct LibvirtClient binding (richer async + no
// process-per-call overhead) without changing the IVirshProcessRunner
// surface.
//
// Lifecycle. The runner is registered as a singleton — every call
// creates its own Process, so the runner itself stays stateless
// and thread-safe by construction. The host's libvirt URI + the
// configured `virsh` binary path are pulled from
// IOptionsMonitor<LibvirtComputeOptions> on every invocation (the
// monitor pattern, not IOptions<T>.Value — the runner sees config
// reloads without a host restart).
//
// Timeouts. Each call enforces a hard timeout from
// LibvirtComputeOptions.TimeoutSeconds. The default 60s is enough
// for `start` / `shutdown` / `domstate`; `virt-install` (the create
// path) can take longer on disk-attached imports, so the timeout
// cap (600s) is set with that in mind.
// ============================================================================

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plexor.Modules.Clusters.Application.Compute;
using Plexor.Shared.Kernel.Compute;

namespace Plexor.Modules.Clusters.Infrastructure.Compute;

/// <summary>
///     Process-shell-out implementation of <see cref="IVirshProcessRunner" />.
///     Spawns <c>virsh</c> per call, captures stdout / stderr, enforces
///     the configured timeout, and translates every failure mode into a
///     typed <see cref="ComputeProviderException" /> with a canonical
///     error code from <see cref="ComputeProviderErrorCodes" />.
/// </summary>
/// <param name="options">
///     Bound <see cref="LibvirtComputeOptions" />. The runner reads
///     <see cref="IOptionsMonitor{TOptions}.CurrentValue" /> on every
///     call so config reloads (rotation of the connection URI,
///     timeout tweaks) take effect without a host restart.
/// </param>
/// <param name="logger">
///     Structured logger for the per-call debug trail (subcommand +
///     exit code). Errors are logged at the provider layer, not here
///     — the runner's only job is to throw the right exception type.
/// </param>
/// <remarks>
///     <para><b>Why a separate runner.</b> Extracting the
///     <c>Process.Start</c> shell-out behind an interface lets the
///     IComputeProvider tests substitute the runner and assert the
///     exact arg list without spinning up a real <c>virsh</c>. The
///     runner's own tests would need a live libvirt (out of scope
///     for unit tests; covered by the manual smoke script).</para>
///     <para><b>Why ProcessStartInfo.ArgumentList and not Arguments.</b>
///     ArgumentList inserts arguments verbatim (no shell quoting); the
///     single-string <c>Arguments</c> form runs through Windows' command-
///     line parsing, which silently mangles flags with embedded quotes
///     or spaces. virsh flag values (disk paths, network names) often
///     contain spaces; ArgumentList is the only safe form.</para>
///     <para><b>Why no private methods.</b> Process-lifecycle
///     helpers (timeout-bound wait, best-effort kill) live in
///     <see cref="LibvirtProcessHelpers" /> per
///     class-layout-and-tooling.md §1a.</para>
/// </remarks>
public sealed class VirshProcessRunner(
    IOptionsMonitor<LibvirtComputeOptions> options,
    ILogger<VirshProcessRunner> logger) : IVirshProcessRunner
{
    /// <inheritdoc />
    public async Task<string> RunAsync(
        string subcommand,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var current = options.CurrentValue;
        var psi = new ProcessStartInfo
        {
            FileName = current.VirshPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(current.ConnectionUri);
        psi.ArgumentList.Add(subcommand);
        foreach (var argument in args)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi) ?? throw new ComputeProviderException(
            ComputeProviderErrorCodes.Unreachable,
            $"virsh: failed to start process (FileName='{current.VirshPath}').");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                stdout.AppendLine(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                stderr.AppendLine(eventArgs.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timeout = TimeSpan.FromSeconds(current.TimeoutSeconds);
        var exited = await LibvirtProcessHelpers
            .WaitForExitOrKillAsync(process, timeout, cancellationToken)
            ;
        if (!exited)
        {
            throw new ComputeProviderException(
                ComputeProviderErrorCodes.Timeout,
                $"virsh {subcommand} exceeded {current.TimeoutSeconds}s timeout.");
        }

        if (process.ExitCode != 0)
        {
            logger.LogDebug(
                "virsh {Subcommand} exited {ExitCode}: {Stderr}",
                subcommand,
                process.ExitCode,
                stderr.ToString());
            throw new ComputeProviderException(
                LibvirtProcessHelpers.MapVirshExitCode(process.ExitCode),
                string.Concat(
                    $"virsh {subcommand} failed (exit {process.ExitCode}): ",
                    stderr.ToString().TrimEnd()));
        }

        return stdout.ToString().TrimEnd();
    }
}
