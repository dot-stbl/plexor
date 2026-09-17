// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeHardwareSpecBuilderShould — unit tests for the wire-shape
// hardware conversion (CpuCores/RamBytes/DiskBytes → Vcpu/RamGb/DiskGb
// /Providers). Pins the round-up-to-gibibyte semantics so a future
// refactor of NodeHardwareSpecBuilder doesn't silently regress the
// rounding mode (truncation would lose capacity info on odd-byte
// totals).
// ============================================================================

using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit;

public sealed class NodeHardwareSpecBuilderShould
{
    [Fact(DisplayName = "Given 8 GB of RAM, when Build, then RamGb rounds up to 8")]
    public void RoundUpRam()
    {
        var spec = NodeHardwareSpecBuilder.Build(
            cpuCores: 4,
            ramBytes: 8L * 1024 * 1024 * 1024,
            diskBytes: 100L * 1024 * 1024 * 1024);

        spec.RamGb.ShouldBe(8);
    }

    [Fact(DisplayName = "Given 8 GB + 1 byte of RAM, when Build, then RamGb rounds up to 9")]
    public void RoundUpRamOddByte()
    {
        var spec = NodeHardwareSpecBuilder.Build(
            cpuCores: 4,
            ramBytes: 8L * 1024 * 1024 * 1024 + 1,
            diskBytes: 100L * 1024 * 1024 * 1024);

        spec.RamGb.ShouldBe(9);
    }

    [Fact(DisplayName = "Given 100 GB of disk, when Build, then DiskGb rounds up to 100")]
    public void RoundUpDisk()
    {
        var spec = NodeHardwareSpecBuilder.Build(
            cpuCores: 8,
            ramBytes: 16L * 1024 * 1024 * 1024,
            diskBytes: 100L * 1024 * 1024 * 1024);

        spec.DiskGb.ShouldBe(100);
    }

    [Fact(DisplayName = "Given any input, when Build, then Vcpu echoes CpuCores verbatim")]
    public void VcpuEqualsCpuCores()
    {
        var spec = NodeHardwareSpecBuilder.Build(
            cpuCores: 12,
            ramBytes: 8L * 1024 * 1024 * 1024,
            diskBytes: 100L * 1024 * 1024 * 1024);

        spec.Vcpu.ShouldBe(12);
    }

    [Fact(DisplayName = "Given any input, when Build, then Providers is empty for v0.1")]
    public void ProvidersEmptyForV01()
    {
        var spec = NodeHardwareSpecBuilder.Build(4, 0, 0);

        spec.Providers.ShouldBeEmpty();
    }
}