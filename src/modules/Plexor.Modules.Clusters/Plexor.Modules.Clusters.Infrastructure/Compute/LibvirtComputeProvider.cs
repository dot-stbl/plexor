// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtComputeProvider — IComputeProvider implementation that
// drives the libvirt hypervisor via the `virsh` / `virt-install`
// CLI. Translates the host's VM-lifecycle intent into the
// underlying runtime's CLI dialect and reports success / failure
// through the canonical ComputeProviderException codes.
//
// Why virsh and not a direct binding. v0.1 ships the
// Process-shell-out path because it works on every supported
// distro without a native package dependency. v0.2+ swaps the
// IVirshProcessRunner impl for a LibvirtClient binding (richer
// async, no per-call process spawn) without touching the
// provider surface — the abstraction is the seam.
//
// Selection. The DI registration in
// ClustersInfrastructureInstaller.AddClustersInfrastructureCore
// inspects the [Clusters:Compute:Libvirt] section's existence
// (configuration.GetSection(...).Exists()) and picks this
// provider over NoOp when the section is present. Absence keeps
// NoOp as the default for dev / self-hosted deployments.
//
// Idempotency. Start/Stop/Delete are best-effort idempotent —
// virsh treats a "start an already-running domain" as a non-zero
// exit (and a duplicate-undefine as such too), but the
// ComputeProviderException contract says the host should treat
// repeated calls as success. We narrow the catch to the
// duplicate-already-in-state codes and swallow them; any other
// error propagates.
// ============================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plexor.Modules.Clusters.Application.Compute;
using Plexor.Shared.Kernel.Compute;

namespace Plexor.Modules.Clusters.Infrastructure.Compute;

/// <summary>
///     IComputeProvider that drives the libvirt hypervisor via the
///     <c>virsh</c> and <c>virt-install</c> CLI. Registered as a
///     singleton in <c>ClustersInfrastructureInstaller</c>; selected
///     when the <c>[Clusters:Compute:Libvirt]</c> TOML section is
///     present in the host configuration.
/// </summary>
/// <param name="options">
///     Bound <see cref="LibvirtComputeOptions" />. Read on every
///     call so config reloads (URI rotation, timeout tweaks) take
///     effect without a host restart.
/// </param>
/// <param name="runner">
///     Test seam over the <c>virsh</c> Process.Start call. In
///     production this resolves to <see cref="VirshProcessRunner" />;
///     in tests it's an NSubstitute mock that captures the exact
///     subcommand + arg list without spawning a real process.
/// </param>
/// <param name="logger">
///     Structured logger. Successful create logs at Information
///     (audit-friendly), per-call tracing stays at Debug (consumer
///     can enable for diagnose-only sessions).
/// </param>
/// <remarks>
///     <para><b>Why each method takes the options snapshot via
///     CurrentValue.</b> Same reason as
///     <see cref="VirshProcessRunner" />: IOptionsMonitor lets the
///     host rotate the libvirt URI without a restart. We pay one
///     extra virtual dispatch per call for hot-reload.</para>
///     <para><b>Why no private methods.</b> Pure-function helpers
///     (SanitizeName, ParseVmPowerState) live in
///     <see cref="LibvirtProcessHelpers" /> per
///     class-layout-and-tooling.md §1a. The provider's body is a
///     flat composition of arguments → runner call.</para>
/// </remarks>
public sealed class LibvirtComputeProvider(
    IOptionsMonitor<LibvirtComputeOptions> options,
    IVirshProcessRunner runner,
    ILogger<LibvirtComputeProvider> logger) : IComputeProvider
{
    /// <inheritdoc />
    public string Name => "libvirt";

    /// <inheritdoc />
    public async Task<string> CreateVmAsync(
        CreateVmRequest request,
        CancellationToken cancellationToken = default)
    {
        var domainName = LibvirtProcessHelpers.SanitizeName(request.Name);
        var vmUuid = Guid.NewGuid().ToString();

        var args = new List<string>
        {
            $"--name={domainName}",
            $"--vcpus={request.Vcpu}",
            $"--memory={request.MemoryBytes / (1024 * 1024)}",
            $"--uuid={vmUuid}",
            "--os-variant=unknown",
            "--import",
            "--noautoconsole",
            "--noreboot",
        };
        foreach (var disk in request.DiskPaths)
        {
            args.Add($"--disk={disk}");
        }
        foreach (var network in request.NetworkNames)
        {
            args.Add($"--network=network={network}");
        }

        await runner.RunAsync("virt-install", args, cancellationToken);

        logger.LogInformation(
            "LibvirtComputeProvider: created domain {DomainName} ({DomainUuid}) at {ConnectionUri}",
            domainName,
            vmUuid,
            options.CurrentValue.ConnectionUri);
        return vmUuid;
    }

    /// <inheritdoc />
    public async Task StartVmAsync(
        string providerVmId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await runner.RunAsync("start", [providerVmId], cancellationToken);
        }
        catch (ComputeProviderException exception) when (exception.Code == ComputeProviderErrorCodes.RequestInvalid)
        {
            // virsh "start <running>" exits non-zero with "domain is already active" —
            // the idempotent case. Swallow: the host contract says the second
            // call against an already-running VM is a successful no-op.
            logger.LogDebug(
                "LibvirtComputeProvider: start on already-running domain {DomainId} — treated as success",
                providerVmId);
        }
    }

    /// <inheritdoc />
    public async Task StopVmAsync(
        string providerVmId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await runner.RunAsync("shutdown", [providerVmId], cancellationToken)
                ;
        }
        catch (ComputeProviderException exception) when (exception.Code == ComputeProviderErrorCodes.RequestInvalid)
        {
            // virsh "shutdown <stopped>" exits non-zero with "domain is not running" —
            // the idempotent case. Swallow: the host contract says the second
            // call against an already-stopped VM is a successful no-op.
            logger.LogDebug(
                "LibvirtComputeProvider: stop on already-stopped domain {DomainId} — treated as success",
                providerVmId);
        }
    }

    /// <inheritdoc />
    public async Task DeleteVmAsync(
        string providerVmId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await runner.RunAsync(
                "undefine",
                [providerVmId, "--remove-all-storage"],
                cancellationToken);
        }
        catch (ComputeProviderException exception) when (exception.Code == ComputeProviderErrorCodes.NotFound)
        {
            // virsh "undefine <unknown>" exits 42 (NotFound) — the idempotent
            // case. Swallow: the host contract says the second call against an
            // already-deleted VM is a successful no-op.
            logger.LogDebug(
                "LibvirtComputeProvider: delete on unknown domain {DomainId} — treated as success",
                providerVmId);
        }
    }

    /// <inheritdoc />
    public async Task<VmPowerState> GetPowerStateAsync(
        string providerVmId,
        CancellationToken cancellationToken = default)
    {
        var output = await runner.RunAsync("domstate", [providerVmId], cancellationToken)
            ;
        return LibvirtProcessHelpers.ParseVmPowerState(output);
    }
}
