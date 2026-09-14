// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeHardware — hardware probe values reported by the agent on
// join and on every heartbeat. Extracted from NodeContracts.cs
// (Sprint 3, item 5) per folder-organization.md §1.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
///     Hardware probe values reported by the agent on join and on
///     every heartbeat. The control plane uses these for capacity
///     planning and to display the node list in the UI.
/// </summary>
/// <param name="CpuCores"></param>
/// <param name="RamBytes"></param>
/// <param name="DiskBytes">
///     Root filesystem capacity by default; the agent exposes a
///     single number for v0.1 and the control plane treats it as
///     advisory (not a reservation limit).
/// </param>
/// <param name="Hostname"></param>
/// <param name="KernelVersion"></param>
public sealed record NodeHardware(
    int CpuCores,
    long RamBytes,
    long DiskBytes,
    string Hostname,
    string KernelVersion);
