// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtComputeProvider tests — the real IComputeProvider that
// drives the libvirt hypervisor via `virsh` / `virt-install`. The
// tests substitute IVirshProcessRunner (NSubstitute) so we can
// assert the exact subcommand + args without spawning a real
// Process; the runner itself is unit-tested via integration smoke
// scripts in the libvirt cluster (out of scope for this project).
//
// Why interface mocks + not a Process spy. The Process class is
// sealed and exposes 30+ APIs; mocking it at the Process level
// means either (a) fragile wrappers or (b) low-level handle
// inspection. The IVirshProcessRunner seam keeps the contract
// testable at the same layer as the production code uses it.
// ============================================================================

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Plexor.Modules.Clusters.Application.Compute;
using Plexor.Modules.Clusters.Infrastructure.Compute;
using Plexor.Shared.Kernel.Compute;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Compute;

public sealed class LibvirtComputeProviderShould
{
    private readonly IVirshProcessRunner _runner = Substitute.For<IVirshProcessRunner>();
    private readonly IOptionsMonitor<LibvirtComputeOptions> _options = Substitute.For<IOptionsMonitor<LibvirtComputeOptions>>();
    private readonly LibvirtComputeProvider _sut;

    public LibvirtComputeProviderShould()
    {
        _options.CurrentValue.Returns(new LibvirtComputeOptions
        {
            ConnectionUri = "qemu:///system",
            VirshPath = "virsh",
            TimeoutSeconds = 60,
        });
        _sut = new LibvirtComputeProvider(_options, _runner, NullLogger<LibvirtComputeProvider>.Instance);
    }

    [Fact(DisplayName = "Given a VM spec, when CreateVmAsync, then emits a virt-install command with the expected flags")]
    public async Task CreateVmAsync_GeneratesValidVirtInstallArgsAsync()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("");

        var handle = await _sut.CreateVmAsync(
            new CreateVmRequest(
                Name: "web-prod-01",
                Vcpu: 4,
                MemoryBytes: 4L * 1024L * 1024L * 1024L,
                DiskPaths: ["/var/lib/plexor/web-prod-01.qcow2"],
                NetworkNames: ["default"]),
            CancellationToken.None);

        // Returns a non-empty UUID-style handle.
        handle.ShouldNotBeNullOrEmpty();

        await _runner.Received(1).RunAsync(
            "virt-install",
            Arg.Is<IReadOnlyList<string>>(args =>
                args.Contains("--name=web-prod-01")
                && args.Contains("--vcpus=4")
                && args.Contains("--memory=4096")
                && args.Contains("--os-variant=unknown")
                && args.Contains("--import")
                && args.Contains("--noautoconsole")
                && args.Contains("--noreboot")
                && args.Contains("--disk=/var/lib/plexor/web-prod-01.qcow2")
                && args.Contains("--network=network=default")
                && args.Any(static arg => arg.StartsWith("--uuid=", StringComparison.Ordinal))),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a domain id, when StartVmAsync, then invokes virsh start <id>")]
    public async Task StartVmAsync_CallsVirshStartWithDomainIdAsync()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("");

        await _sut.StartVmAsync("uuid-1234", CancellationToken.None);

        await _runner.Received(1).RunAsync(
            "start",
            Arg.Is<IReadOnlyList<string>>(args => args.Count == 1 && args[0] == "uuid-1234"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a domain id, when StopVmAsync, then invokes virsh shutdown <id>")]
    public async Task StopVmAsync_CallsVirshShutdownWithDomainIdAsync()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("");

        await _sut.StopVmAsync("uuid-1234", CancellationToken.None);

        await _runner.Received(1).RunAsync(
            "shutdown",
            Arg.Is<IReadOnlyList<string>>(args => args.Count == 1 && args[0] == "uuid-1234"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a domain id, when DeleteVmAsync, then invokes virsh undefine <id> --remove-all-storage")]
    public async Task DeleteVmAsync_CallsVirshUndefineWithRemoveAllStorageAsync()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("");

        await _sut.DeleteVmAsync("uuid-1234", CancellationToken.None);

        await _runner.Received(1).RunAsync(
            "undefine",
            Arg.Is<IReadOnlyList<string>>(args =>
                args.Count == 2
                && args[0] == "uuid-1234"
                && args[1] == "--remove-all-storage"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given a runner returning 'running', when GetPowerStateAsync, then maps to Running")]
    public async Task GetPowerStateAsync_RunningMapsToRunningStateAsync()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("running");

        var state = await _sut.GetPowerStateAsync("uuid-1234", CancellationToken.None);

        state.ShouldBe(VmPowerState.Running);
    }

    [Fact(DisplayName = "Given a runner returning 'shut off', when GetPowerStateAsync, then maps to Stopped")]
    public async Task GetPowerStateAsync_ShutOffMapsToStoppedStateAsync()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("shut off");

        var state = await _sut.GetPowerStateAsync("uuid-1234", CancellationToken.None);

        state.ShouldBe(VmPowerState.Stopped);
    }

    [Fact(DisplayName = "Given a runner returning 'paused', when GetPowerStateAsync, then maps to Unknown")]
    public async Task GetPowerStateAsync_UnknownMapsToUnknownStateAsync()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("paused");

        var state = await _sut.GetPowerStateAsync("uuid-1234", CancellationToken.None);

        state.ShouldBe(VmPowerState.Unknown);
    }

    [Fact(DisplayName = "Given a name with special characters, when CreateVmAsync, then sanitises the domain name")]
    public async Task CreateVmAsync_SanitisesDomainNameAsync()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("");

        await _sut.CreateVmAsync(
            new CreateVmRequest(
                Name: "web prod 01!@#",
                Vcpu: 1,
                MemoryBytes: 1024 * 1024 * 1024,
                DiskPaths: [],
                NetworkNames: []),
            CancellationToken.None);

        // The runner should have received the sanitised domain name
        // (libvirt allows only ASCII letters / digits / dashes /
        // underscores; spaces and punctuation become underscores,
        // and trailing underscores are trimmed). Input "web prod 01!@#"
        // → "web_prod_01" (the three trailing underscores collapse
        // to nothing under Trim).
        await _runner.Received(1).RunAsync(
            "virt-install",
            Arg.Is<IReadOnlyList<string>>(args =>
                args.Contains("--name=web_prod_01")),
            Arg.Any<CancellationToken>());
    }
}
