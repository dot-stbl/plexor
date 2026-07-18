// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadKindMapperShould — bidirectional mapping between
// sealed-record WorkloadKind hierarchy and wire-format strings.
// Tier 1 test coverage for the new DockerCompose / PodmanQuadlet /
// K3s kinds added in the runtime-providers plan. All existing
// kinds (Vm, Lxc, Qemu, K8sPod, Container) are still in scope so
// a rename in the sealed-record type stays caught here.
// ============================================================================

using Plexor.Shared.NodeApi;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.WorkloadKinds;

public sealed class WorkloadKindMapperShould
{
    [Theory(DisplayName = "Given a known wire name, when FromName, then returns matching sealed record")]
    [InlineData("vm", "vm")]
    [InlineData("lxc", "lxc")]
    [InlineData("qemu", "qemu")]
    [InlineData("k8s.pod", "k8s.pod")]
    [InlineData("container", "container")]
    [InlineData("docker-compose", "docker-compose")]
    [InlineData("podman-quadlet", "podman-quadlet")]
    [InlineData("k3s", "k3s")]
    public void ReturnSealedRecordForKnownName(string wireName, string expectedName)
    {
        var result = WorkloadKindMapper.FromName(wireName);

        result.ShouldNotBeNull();
        result.Name.ShouldBe(expectedName);
    }

    [Theory(DisplayName = "Given a sealed record, when ToName, then returns matching wire name")]
    [InlineData("vm")]
    [InlineData("lxc")]
    [InlineData("qemu")]
    [InlineData("k8s.pod")]
    [InlineData("container")]
    [InlineData("docker-compose")]
    [InlineData("podman-quadlet")]
    [InlineData("k3s")]
    public void ReturnWireNameForSealedRecord(string wireName)
    {
        var kindRecord = WorkloadKindMapper.FromName(wireName);

        WorkloadKindMapper.ToName(kindRecord!).ShouldBe(wireName);
    }

    [Fact(DisplayName = "Given null input, when FromName, then returns null")]
    public void ReturnNullForNullInput()
    {
        WorkloadKindMapper.FromName(null).ShouldBeNull();
    }

    [Fact(DisplayName = "Given unknown wire name, when FromName, then throws NotSupportedException")]
    public void ThrowOnUnknownName()
    {
        var ex = Should.Throw<NotSupportedException>(static () =>
            WorkloadKindMapper.FromName("non-existent-runtime"));

        ex.Message.ShouldContain("non-existent-runtime");
        ex.Message.ShouldContain("docker-compose");
        ex.Message.ShouldContain("podman-quadlet");
        ex.Message.ShouldContain("k3s");
    }

    [Fact(DisplayName = "Given sealed records, when round-tripped, then they preserve identity by wire name")]
    public void RoundTripPreservesIdentity()
    {
        // Round-trip via the wire name; the sealed records' identity
        // (==) is preserved only via .Name because the records are
        // sealed without value-equality overrides.
        var kinds = new WorkloadKind[]
        {
            new WorkloadKind.Vm(),
            new WorkloadKind.Lxc(),
            new WorkloadKind.Qemu(),
            new WorkloadKind.K8sPod(),
            new WorkloadKind.Container(),
            new WorkloadKind.DockerCompose(),
            new WorkloadKind.PodmanQuadlet(),
            new WorkloadKind.K3s(),
        };

        foreach (var kind in kinds)
        {
            var wireName = WorkloadKindMapper.ToName(kind);
            var roundTripped = WorkloadKindMapper.FromName(wireName);
            roundTripped!.Name.ShouldBe(kind.Name);
        }
    }
}
