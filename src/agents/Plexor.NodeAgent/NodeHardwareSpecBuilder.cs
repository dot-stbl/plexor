// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeHardwareSpecBuilder — pure helper that turns the agent's
// OS-probed hardware (CpuCores / RamBytes / DiskBytes) into the wire
// NodeHardwareSpec (Vcpu / RamGb / DiskGb / Providers).
//
// Extracted as a separate static class (Sep 2026) so both the joiner
// and the heartbeat loop can share the conversion without pulling
// private helpers into their respective files (§1a forbids private
// methods on production classes).
// ============================================================================

using Plexor.Shared.NodeApi;

namespace Plexor.NodeAgent;

/// <summary>
///     Hardware snapshot builder shared by
///     <see cref="NodeJoiner" /> (register) and
///     <see cref="NodeHeartbeatLoop" /> (every 30 s heartbeat). RAM
///     and disk bytes are rounded up to gibibytes; the
///     <see cref="NodeHardwareSpec.Providers" /> list is empty for v0.1
///     — a future iteration populates it from the provider registry.
/// </summary>
internal static class NodeHardwareSpecBuilder
{
    /// <summary>Build a wire <see cref="NodeHardwareSpec" /> from the
    /// agent's OS-probed values.</summary>
    /// <param name="cpuCores">Logical CPU count (Environment.ProcessorCount).</param>
    /// <param name="ramBytes">Total RAM in bytes.</param>
    /// <param name="diskBytes">Total block storage in bytes.</param>
    public static NodeHardwareSpec Build(
        int cpuCores,
        long ramBytes,
        long diskBytes)
    {
        return new NodeHardwareSpec(
            Vcpu: cpuCores,
            RamGb: (int)Math.Ceiling(ramBytes / (double)(1024L * 1024L * 1024L)),
            DiskGb: (int)Math.Ceiling(diskBytes / (double)(1024L * 1024L * 1024L)),
            Providers: []);
    }
}