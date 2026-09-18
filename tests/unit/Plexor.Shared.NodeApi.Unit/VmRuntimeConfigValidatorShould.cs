// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VmRuntimeConfigValidatorShould — unit tests for the wire-stable
// VM runtime config validator. Pure function: no I/O, no DI,
// no fixture. Every test is one arrange-act-assert against the
// static Validate(...) entry point.
// ==========================================================================

using Shouldly;
using Xunit;

namespace Plexor.Shared.NodeApi.Unit;

public sealed class VmRuntimeConfigValidatorShould
{
    [Fact(DisplayName = "Given a default-valid config, when Validate, then IsValid is true with no errors")]
    public void DefaultValidConfigPassesValidation()
    {
        var config = NewValidConfig();

        var result = VmRuntimeConfigValidator.Validate(config);

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Theory(DisplayName = "Given Vcpu out of range, when Validate, then IsValid is false with one error naming the field")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(257)]
    [InlineData(int.MaxValue)]
    public void VcpuOutOfRangeFails(int vcpu)
    {
        var config = NewValidConfig() with { Vcpu = vcpu };

        var result = VmRuntimeConfigValidator.Validate(config);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldContain("Vcpu");
    }

    [Theory(DisplayName = "Given Vcpu at the boundary, when Validate, then IsValid is true")]
    [InlineData(1)]
    [InlineData(256)]
    public void VcpuAtBoundaryPasses(int vcpu)
    {
        var config = NewValidConfig() with { Vcpu = vcpu };

        VmRuntimeConfigValidator.Validate(config).IsValid.ShouldBeTrue();
    }

    [Theory(DisplayName = "Given RamBytes out of range, when Validate, then IsValid is false with one error naming the field")]
    [InlineData(0L)]
    [InlineData(512L * 1024 * 1024 - 1)]
    [InlineData(1024L * 1024 * 1024 * 1024 + 1)]
    public void RamBytesOutOfRangeFails(long ramBytes)
    {
        var config = NewValidConfig() with { RamBytes = ramBytes };

        var result = VmRuntimeConfigValidator.Validate(config);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldContain("RamBytes");
    }

    [Theory(DisplayName = "Given DiskBytes out of range, when Validate, then IsValid is false with one error naming the field")]
    [InlineData(0L)]
    [InlineData(1024L * 1024 * 1024 - 1)]
    [InlineData(10L * 1024 * 1024 * 1024 * 1024 + 1)]
    public void DiskBytesOutOfRangeFails(long diskBytes)
    {
        var config = NewValidConfig() with { DiskBytes = diskBytes };

        var result = VmRuntimeConfigValidator.Validate(config);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldContain("DiskBytes");
    }

    [Theory(DisplayName = "Given an empty/whitespace ImageRef, when Validate, then IsValid is false")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void EmptyImageRefFails(string imageRef)
    {
        var config = NewValidConfig() with { ImageRef = imageRef };

        var result = VmRuntimeConfigValidator.Validate(config);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(static e => e.Contains("ImageRef"));
    }

    [Fact(DisplayName = "Given NetworkName with invalid chars, when Validate, then IsValid is false")]
    public void InvalidNetworkNameFails()
    {
        var config = NewValidConfig() with { NetworkName = "bad name with spaces" };

        var result = VmRuntimeConfigValidator.Validate(config);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(static e => e.Contains("NetworkName"));
    }

    [Fact(DisplayName = "Given NetworkName longer than 16 chars, when Validate, then IsValid is false")]
    public void OverlongNetworkNameFails()
    {
        var config = NewValidConfig() with { NetworkName = "this-name-is-too-long" };

        var result = VmRuntimeConfigValidator.Validate(config);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(static e => e.Contains("NetworkName"));
    }

    [Theory(DisplayName = "Given a valid NetworkName, when Validate, then IsValid is true")]
    [InlineData("br0")]
    [InlineData("default")]
    [InlineData("prod-vpc_01")]
    [InlineData("a")]
    public void ValidNetworkNamePasses(string networkName)
    {
        var config = NewValidConfig() with { NetworkName = networkName };

        VmRuntimeConfigValidator.Validate(config).IsValid.ShouldBeTrue();
    }

    [Fact(DisplayName = "Given NetworkName null, when Validate, then IsValid is true (null = default bridge)")]
    public void NullNetworkNamePasses()
    {
        var config = NewValidConfig() with { NetworkName = null };

        VmRuntimeConfigValidator.Validate(config).IsValid.ShouldBeTrue();
    }

    [Fact(DisplayName = "Given multiple violations, when Validate, then Errors lists ALL of them")]
    public void MultipleViolationsAreAggregated()
    {
        var config = new VmRuntimeConfig(
            Vcpu: 0,
            RamBytes: 0L,
            DiskBytes: 0L,
            ImageRef: "",
            NetworkName: "bad name",
            SshKeyFingerprint: "");

        var result = VmRuntimeConfigValidator.Validate(config);

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(6);
        result.Errors.ShouldContain(static e => e.Contains("Vcpu"));
        result.Errors.ShouldContain(static e => e.Contains("RamBytes"));
        result.Errors.ShouldContain(static e => e.Contains("DiskBytes"));
        result.Errors.ShouldContain(static e => e.Contains("ImageRef"));
        result.Errors.ShouldContain(static e => e.Contains("NetworkName"));
        result.Errors.ShouldContain(static e => e.Contains("SshKeyFingerprint"));
    }

    private static VmRuntimeConfig NewValidConfig()
    {
        return new VmRuntimeConfig(
            Vcpu: 2,
            RamBytes: 4L * 1024 * 1024 * 1024,
            DiskBytes: 40L * 1024 * 1024 * 1024,
            ImageRef: "ubuntu-22.04-cloud",
            NetworkName: "br0");
    }
}
