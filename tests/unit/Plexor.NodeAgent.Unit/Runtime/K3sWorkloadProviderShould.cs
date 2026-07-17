// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// K3sWorkloadProviderShould — IWorkloadProvider behavior tests.
// IKubectlCliRunner is replaced with NSubstitute mocks so the
// tests stay hermetic (no real kubectl binary needed).
//
// What we cover:
//   - CreateAsync writes kustomization + deployment (+ optional
//     service) files, shells `kubectl apply -k <dir>`.
//   - StartAsync scales the deployment to the configured replicas.
//   - StopAsync scales the deployment to 0 replicas (k8s idiom).
//   - DeleteAsync shells `kubectl delete -k --ignore-not-found` + rm.
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

public sealed class K3sWorkloadProviderShould
{
    private const string ManifestsDirectory = "/var/lib/plexor/workloads/k3s";

    [Fact(DisplayName = "Given a valid spec, when CreateAsync, then writes manifest files + kubectl apply -k")]
    public async Task CreateAsyncWritesManifestAndShellsKubectlApply()
    {
        var kubectl = Substitute.For<IKubectlCliRunner>();
        kubectl.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("deployment.apps/web created");
        var clock = Substitute.For<TimeProvider>();
        var fixedNow = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        clock.GetUtcNow().Returns(fixedNow);

        var logger = new NullLogger<K3sWorkloadProvider>();
        var sut = new K3sWorkloadProvider(kubectl, clock, logger);

        var spec = new WorkloadSpec(
            Kind: new WorkloadKind.K3s(),
            Name: "web",
            Config: JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["image"] = "nginx:1.25",
                ["ports"] = SinglePort80,
            }));

        var local = await sut.CreateAsync(spec, CancellationToken.None);

        // State contract
        local.Kind.ShouldBe(new WorkloadKind.K3s());
        local.Name.ShouldBe("web");
        local.State.ShouldBe(WorkloadState.Running);

        // kubectl apply -k was called exactly once.
        await kubectl.Received(1).RunAsync(
            Arg.Is<string>(args => args.StartsWith("apply -k ", StringComparison.Ordinal) &&
                                  args.EndsWith("/web", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());

        // The three manifest files now exist (no service.yaml —
        // ports exposed but we'll re-check below).
        var dir = $"{ManifestsDirectory}/web";
        File.Exists($"{dir}/kustomization.yaml").ShouldBeTrue();
        File.Exists($"{dir}/deployment.yaml").ShouldBeTrue();
        File.Exists($"{dir}/service.yaml").ShouldBeTrue();

        var kustomization = await File.ReadAllTextAsync($"{dir}/kustomization.yaml", CancellationToken.None);
        kustomization.ShouldContain("namespace: default");
        kustomization.ShouldContain("- deployment.yaml");
        kustomization.ShouldContain("- service.yaml");

        var deployment = await File.ReadAllTextAsync($"{dir}/deployment.yaml", CancellationToken.None);
        deployment.ShouldContain("Image: nginx:1.25");
        deployment.ShouldContain("containerPort: 80");

        CleanupWorkloadDir("web");
    }

    [Fact(DisplayName = "Given a created workload, when StartAsync, then kubectl scale back to configured replicas")]
    public async Task StartAsyncShellsScaleUp()
    {
        var (sut, kubectl, list) = await CreateProviderWithWorkloadAsync();

        await sut.StartAsync(list[0].Id, CancellationToken.None);

        // Exactly one scale call (StopAsync below scales to 0;
        // CreateAsync implicitly applied the manifest which
        // doesn't count as a scale call).
        await kubectl.Received(1).RunAsync(
            Arg.Is<string>(args => args.StartsWith("scale deployment/web --replicas=", StringComparison.Ordinal) &&
                                  args.EndsWith('1')),
            Arg.Any<CancellationToken>());

        CleanupWorkloadDir("web");
    }

    [Fact(DisplayName = "Given a created workload, when StopAsync, then kubectl scale to 0 and flips state")]
    public async Task StopAsyncShellsScaleToZeroAndFlipsState()
    {
        var (sut, kubectl, list) = await CreateProviderWithWorkloadAsync();

        var stopped = await sut.StopAsync(list[0].Id, CancellationToken.None);

        stopped.State.ShouldBe(WorkloadState.Stopped);
        await kubectl.Received(1).RunAsync(
            "scale deployment/web --replicas=0",
            Arg.Any<CancellationToken>());

        CleanupWorkloadDir("web");
    }

    [Fact(DisplayName = "Given a created workload, when DeleteAsync, then kubectl delete -k + rm -r")]
    public async Task DeleteAsyncShellsDeleteAndRemovesManifestDir()
    {
        var (sut, kubectl, list) = await CreateProviderWithWorkloadAsync();

        await sut.DeleteAsync(list[0].Id, CancellationToken.None);

        await kubectl.Received(1).RunAsync(
            Arg.Is<string>(args => args.StartsWith("delete -k ", StringComparison.Ordinal) &&
                                  args.Contains("--ignore-not-found", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());

        Directory.Exists($"{ManifestsDirectory}/web").ShouldBeFalse();
    }

    [Fact(DisplayName = "Given an unknown localId, when DeleteAsync, then is a no-op (idempotent contract)")]
    public async Task DeleteAsyncOnUnknownIdIsNoOp()
    {
        var kubectl = Substitute.For<IKubectlCliRunner>();
        var clock = Substitute.For<TimeProvider>();
        var logger = new NullLogger<K3sWorkloadProvider>();
        var sut = new K3sWorkloadProvider(kubectl, clock, logger);

        var tombstone = await sut.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        tombstone.State.ShouldBe(WorkloadState.Stopped);
        await kubectl.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Fact(DisplayName = "Given an invalid JSON config, when CreateAsync, then throws InvalidOperationException")]
    public async Task CreateAsyncThrowsOnInvalidConfig()
    {
        var kubectl = Substitute.For<IKubectlCliRunner>();
        var clock = Substitute.For<TimeProvider>();
        var logger = new NullLogger<K3sWorkloadProvider>();
        var sut = new K3sWorkloadProvider(kubectl, clock, logger);

        var spec = new WorkloadSpec(
            Kind: new WorkloadKind.K3s(),
            Name: "web",
            Config: JsonSerializer.SerializeToElement(new { ports = SinglePort80 }));
        // Missing required 'image' field.

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateAsync(spec, CancellationToken.None));
        ex.Message.ShouldContain("image");

        await kubectl.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    private static int[] SinglePort80 => [80];

    private static void CleanupWorkloadDir(string name)
    {
        var dir = $"{ManifestsDirectory}/{name}";
        try { Directory.Delete(dir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private static async Task<(
        K3sWorkloadProvider Sut,
        IKubectlCliRunner Kubectl,
        IReadOnlyList<LocalWorkload> List)>
        CreateProviderWithWorkloadAsync()
    {
        var kubectl = Substitute.For<IKubectlCliRunner>();
        kubectl.RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        var clock = Substitute.For<TimeProvider>();
        var fixedNow = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        clock.GetUtcNow().Returns(fixedNow);

        var logger = new NullLogger<K3sWorkloadProvider>();
        var sut = new K3sWorkloadProvider(kubectl, clock, logger);

        var spec = new WorkloadSpec(
            Kind: new WorkloadKind.K3s(),
            Name: "web",
            Config: JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["image"] = "nginx:1.25",
            }));
        await sut.CreateAsync(spec, CancellationToken.None);

        var list = await sut.ListAsync(CancellationToken.None);
        return (sut, kubectl, list);
    }
}
