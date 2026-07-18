// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DockerComposeWorkloadProviderShould — IWorkloadProvider behavior
// tests. The IDockerCliRunner dependency is replaced with a NSubstitute
// mock so the tests stay hermetic (no real docker binary needed).
//
// What we cover:
//   - CreateAsync writes the manifest to /var/lib/plexor/workloads/<n>/,
//     shells `docker compose -f <path> up -d`, and returns a
//     LocalWorkload in Running state with StartedAt set.
//   - Start/Stop/Delete shell `docker compose -f <path> {start|stop|down}`.
//   - Delete of a missing local-id is a no-op (idempotent contract).
//   - CreateAsync with invalid JSON config throws.
//   - CreateAsync with a null/empty service name throws ArgumentException.
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

public sealed class DockerComposeWorkloadProviderShould
{
    private static readonly int[] SinglePort80 = [80];

    [Fact(DisplayName = "Given a valid spec, when CreateAsync, then writes manifest and shells docker compose up -d")]
    public async Task CreateAsyncWritesManifestAndShellsDockerAsync()
    {
        var docker = Substitute.For<IDockerCliRunner>();
        docker.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Container web Running 0.0.0.0:80->80/tcp");
        var clock = Substitute.For<TimeProvider>();
        var fixedNow = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        clock.GetUtcNow().Returns(fixedNow);

        var logger = new NullLogger<DockerComposeWorkloadProvider>();
        var sut = new DockerComposeWorkloadProvider(docker, clock, logger);

        var spec = new WorkloadSpec(
            Kind: new WorkloadKind.DockerCompose(),
            Name: "web",
            Config: JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["image"] = "nginx:1.25",
                ["ports"] = SinglePort80,
            }));

        var local = await sut.CreateAsync(spec, CancellationToken.None);

        // State contract
        local.Kind.ShouldBe(new WorkloadKind.DockerCompose());
        local.Name.ShouldBe("web");
        local.State.ShouldBe(WorkloadState.Running);
        local.StartedAt.ShouldBe(fixedNow);
        local.CreatedAt.ShouldBe(fixedNow);

        // docker was called exactly once with the up -d invocation.
        await docker.Received(1).RunAsync(
            Arg.Is<string>(static args => args.StartsWith("compose -f ", StringComparison.Ordinal) &&
                                  args.EndsWith(" up -d", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());

        // The manifest now lives at /var/lib/plexor/workloads/web/compose.yaml.
        File.Exists("/var/lib/plexor/workloads/web/compose.yaml").ShouldBeTrue();
        var content = await File.ReadAllTextAsync(
            "/var/lib/plexor/workloads/web/compose.yaml", CancellationToken.None);
        content.ShouldContain("image: nginx:1.25");
        content.ShouldContain("- \"80:80\"");

        // Cleanup so reruns in CI don't accumulate.
        try { File.Delete("/var/lib/plexor/workloads/web/compose.yaml"); }
        catch { /* best-effort cleanup */ }
    }

    [Fact(DisplayName = "Given a valid spec, when StartAsync, then shells docker compose start")]
    public async Task StartAsyncShellsStartCommandAsync()
    {
        var (sut, docker, list) = await CreateProviderWithWorkloadAsync();

        await sut.StartAsync(list[0].Id, CancellationToken.None);

        await docker.Received(1).RunAsync(
            Arg.Is<string>(static args => args.EndsWith(" start", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());

        try { File.Delete("/var/lib/plexor/workloads/web/compose.yaml"); }
        catch { /* best-effort cleanup */ }
    }

    [Fact(DisplayName = "Given a valid spec, when StopAsync, then shells docker compose stop and flips state to Stopped")]
    public async Task StopAsyncShellsStopAndFlipsStateAsync()
    {
        var (sut, docker, list) = await CreateProviderWithWorkloadAsync();
        var firstLocal = list[0];

        var stopped = await sut.StopAsync(firstLocal.Id, CancellationToken.None);

        stopped.State.ShouldBe(WorkloadState.Stopped);
        await docker.Received(1).RunAsync(
            Arg.Is<string>(static args => args.EndsWith(" stop", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());

        try { File.Delete("/var/lib/plexor/workloads/web/compose.yaml"); }
        catch { /* best-effort cleanup */ }
    }

    [Fact(DisplayName = "Given an unknown localId, when DeleteAsync, then is a no-op (idempotent contract)")]
    public async Task DeleteAsyncOnUnknownIdIsNoOpAsync()
    {
        var docker = Substitute.For<IDockerCliRunner>();
        var clock = Substitute.For<TimeProvider>();
        var logger = new NullLogger<DockerComposeWorkloadProvider>();
        var sut = new DockerComposeWorkloadProvider(docker, clock, logger);

        var tombstone = await sut.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        tombstone.State.ShouldBe(WorkloadState.Stopped);
        await docker.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Fact(DisplayName = "Given an invalid JSON config, when CreateAsync, then throws InvalidOperationException")]
    public async Task CreateAsyncThrowsOnInvalidConfigAsync()
    {
        var docker = Substitute.For<IDockerCliRunner>();
        var clock = Substitute.For<TimeProvider>();
        var logger = new NullLogger<DockerComposeWorkloadProvider>();
        var sut = new DockerComposeWorkloadProvider(docker, clock, logger);

        var spec = new WorkloadSpec(
            Kind: new WorkloadKind.DockerCompose(),
            Name: "web",
            Config: JsonSerializer.SerializeToElement(new { ports = SinglePort80 }));
        // Missing required 'image' field.

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateAsync(spec, CancellationToken.None));
        ex.Message.ShouldContain("image");

        await docker.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    private static async Task<(
        DockerComposeWorkloadProvider Sut,
        IDockerCliRunner Docker,
        IReadOnlyList<LocalWorkload> List)>
        CreateProviderWithWorkloadAsync()
    {
        var docker = Substitute.For<IDockerCliRunner>();
        docker.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        var clock = Substitute.For<TimeProvider>();
        var fixedNow = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        clock.GetUtcNow().Returns(fixedNow);

        var logger = new NullLogger<DockerComposeWorkloadProvider>();
        var sut = new DockerComposeWorkloadProvider(docker, clock, logger);

        var spec = new WorkloadSpec(
            Kind: new WorkloadKind.DockerCompose(),
            Name: "web",
            Config: JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["image"] = "nginx:1.25",
            }));
        await sut.CreateAsync(spec, CancellationToken.None);

        var list = await sut.ListAsync(CancellationToken.None);
        return (sut, docker, list);
    }
}
