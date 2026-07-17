// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PodmanQuadletWorkloadProviderShould — IWorkloadProvider behavior
// tests. IPodmanCliRunner is replaced with NSubstitute mocks so the
// tests stay hermetic (no real podman / systemctl binary needed).
//
// What we cover:
//   - CreateAsync writes the .container file, shells
//     `systemctl daemon-reload` then `systemctl start <name>.service`,
//     returns a LocalWorkload in Running state.
//   - StartAsync shells `systemctl start <name>.service`.
//   - StopAsync shells `systemctl stop <name>.service` + flips state.
//   - DeleteAsync shells stop + rm + daemon-reload.
//   - Delete of a missing local-id is a no-op (idempotent).
//   - CreateAsync with invalid JSON config throws.
// ============================================================================

using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Plexor.NodeAgent.Providers.Runtime;
using Plexor.Shared.NodeApi;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Runtime;

public sealed class PodmanQuadletWorkloadProviderShould
{
    private const string QuadletsDirectory = "/etc/containers/systemd";

    [Fact(DisplayName = "Given a valid spec, when CreateAsync, then writes .container + daemon-reload + start")]
    public async Task CreateAsyncWritesQuadletAndShellsSystemctl()
    {
        var systemd = Substitute.For<IPodmanCliRunner>();
        systemd.RunSystemctlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        var clock = Substitute.For<TimeProvider>();
        var fixedNow = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        clock.GetUtcNow().Returns(fixedNow);

        var logger = new NullLogger<PodmanQuadletWorkloadProvider>();
        var sut = new PodmanQuadletWorkloadProvider(systemd, clock, logger);

        var spec = new WorkloadSpec(
            Kind: new WorkloadKind.PodmanQuadlet(),
            Name: "web",
            Config: JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["image"] = "nginx:1.25",
                ["ports"] = SinglePort80,
            }));

        var local = await sut.CreateAsync(spec, CancellationToken.None);

        // State contract
        local.Kind.ShouldBe(new WorkloadKind.PodmanQuadlet());
        local.Name.ShouldBe("web");
        local.State.ShouldBe(WorkloadState.Running);
        local.StartedAt.ShouldBe(fixedNow);
        local.CreatedAt.ShouldBe(fixedNow);

        // systemd daemon-reload and start were each called exactly once.
        await systemd.Received(1).RunSystemctlAsync("daemon-reload", Arg.Any<CancellationToken>());
        await systemd.Received(1).RunSystemctlAsync(
            "start web.service",
            Arg.Any<CancellationToken>());

        // podman binary was NOT touched (we don't pull images
        // at create-time; that's the user's responsibility or a
        // separate image-pull worker in Phase 7+).
        await systemd.DidNotReceiveWithAnyArgs().RunPodmanAsync(default!, default);

        // The quadlet file now lives at /etc/containers/systemd/web.container.
        var quadletPath = $"{QuadletsDirectory}/web.container";
        File.Exists(quadletPath).ShouldBeTrue();
        var content = await File.ReadAllTextAsync(quadletPath, CancellationToken.None);
        content.ShouldContain("Image=nginx:1.25");
        content.ShouldContain("PublishPort=80:80");

        // Cleanup so reruns in CI don't accumulate.
        try { File.Delete(quadletPath); }
        catch { /* best-effort cleanup */ }
    }

    [Fact(DisplayName = "Given a created workload, when StartAsync, then shells systemctl start")]
    public async Task StartAsyncShellsStart()
    {
        var (sut, systemd, list) = await CreateProviderWithWorkloadAsync();

        await sut.StartAsync(list[0].Id, CancellationToken.None);

        // Two start invocations total — CreateAsync started the unit
        // at create-time, then StartAsync starts it again.
        await systemd.Received(2).RunSystemctlAsync(
            "start web.service",
            Arg.Any<CancellationToken>());

        CleanupQuadletFile("web");
    }

    [Fact(DisplayName = "Given a created workload, when StopAsync, then shells systemctl stop and flips state to Stopped")]
    public async Task StopAsyncShellsStopAndFlipsState()
    {
        var (sut, systemd, list) = await CreateProviderWithWorkloadAsync();

        var stopped = await sut.StopAsync(list[0].Id, CancellationToken.None);

        stopped.State.ShouldBe(WorkloadState.Stopped);
        await systemd.Received(1).RunSystemctlAsync(
            "stop web.service",
            Arg.Any<CancellationToken>());

        CleanupQuadletFile("web");
    }

    [Fact(DisplayName = "Given a created workload, when DeleteAsync, then stop + rm + daemon-reload")]
    public async Task DeleteAsyncStopsAndRemovesQuadlet()
    {
        var (sut, systemd, list) = await CreateProviderWithWorkloadAsync();
        var quadletPath = $"{QuadletsDirectory}/web.container";

        await sut.DeleteAsync(list[0].Id, CancellationToken.None);

        // systemctl stop + daemon-reload each called once.
        await systemd.Received(1).RunSystemctlAsync(
            "stop web.service",
            Arg.Any<CancellationToken>());
        await systemd.Received(2).RunSystemctlAsync(
            "daemon-reload",
            Arg.Any<CancellationToken>());

        File.Exists(quadletPath).ShouldBeFalse();
    }

    [Fact(DisplayName = "Given an unknown localId, when DeleteAsync, then is a no-op (idempotent contract)")]
    public async Task DeleteAsyncOnUnknownIdIsNoOp()
    {
        var systemd = Substitute.For<IPodmanCliRunner>();
        var clock = Substitute.For<TimeProvider>();
        var logger = new NullLogger<PodmanQuadletWorkloadProvider>();
        var sut = new PodmanQuadletWorkloadProvider(systemd, clock, logger);

        var tombstone = await sut.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        tombstone.State.ShouldBe(WorkloadState.Stopped);
        await systemd.DidNotReceiveWithAnyArgs().RunSystemctlAsync(default!, default);
    }

    [Fact(DisplayName = "Given an invalid JSON config, when CreateAsync, then throws InvalidOperationException")]
    public async Task CreateAsyncThrowsOnInvalidConfig()
    {
        var systemd = Substitute.For<IPodmanCliRunner>();
        var clock = Substitute.For<TimeProvider>();
        var logger = new NullLogger<PodmanQuadletWorkloadProvider>();
        var sut = new PodmanQuadletWorkloadProvider(systemd, clock, logger);

        var spec = new WorkloadSpec(
            Kind: new WorkloadKind.PodmanQuadlet(),
            Name: "web",
            Config: JsonSerializer.SerializeToElement(new { ports = SinglePort80 }));
        // Missing required 'image' field.

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateAsync(spec, CancellationToken.None));
        ex.Message.ShouldContain("image");

        await systemd.DidNotReceiveWithAnyArgs().RunSystemctlAsync(default!, default);
    }

    private static int[] SinglePort80 => [80];

    private static void CleanupQuadletFile(string name)
    {
        var path = $"{QuadletsDirectory}/{name}.container";
        try { File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }

    private static async Task<(
        PodmanQuadletWorkloadProvider Sut,
        IPodmanCliRunner Systemd,
        IReadOnlyList<LocalWorkload> List)>
        CreateProviderWithWorkloadAsync()
    {
        var systemd = Substitute.For<IPodmanCliRunner>();
        systemd.RunSystemctlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        var clock = Substitute.For<TimeProvider>();
        var fixedNow = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        clock.GetUtcNow().Returns(fixedNow);

        var logger = new NullLogger<PodmanQuadletWorkloadProvider>();
        var sut = new PodmanQuadletWorkloadProvider(systemd, clock, logger);

        var spec = new WorkloadSpec(
            Kind: new WorkloadKind.PodmanQuadlet(),
            Name: "web",
            Config: JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["image"] = "nginx:1.25",
            }));
        await sut.CreateAsync(spec, CancellationToken.None);

        var list = await sut.ListAsync(CancellationToken.None);
        return (sut, systemd, list);
    }
}
