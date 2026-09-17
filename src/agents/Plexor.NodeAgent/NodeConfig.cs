// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeConfig — configuration surface for the Plexor.NodeAgent. v0.1
// takes the worker directly; future moves to IOptions<NodeConfig>
// with validation. Extracted from NodeAgentWorker.cs (Sep 2026) so
// the Joiner / HeartbeatLoop / PollLoop collaborators can inject it
// without each taking a parameter off the worker itself.
// ============================================================================

namespace Plexor.NodeAgent;

/// <summary>
///     Configuration surface for the worker. Bound v
///     <c>Plexor:Node</c> in appsettings. v0.1 takes the worker
///     directly; future moves to IOptions&lt;NodeConfig&gt; with
///     validation.
/// </summary>
/// <param name="CpuCores">Logical CPU count (Environment.ProcessorCount).</param>
/// <param name="RamBytes">Total RAM in bytes.</param>
/// <param name="DiskBytes">Total block storage in bytes.</param>
/// <param name="Hostname">OS-reported hostname.</param>
/// <param name="ControlPlaneUrl">Control-plane root URL.</param>
public sealed record NodeConfig(
    int CpuCores,
    long RamBytes,
    long DiskBytes,
    string Hostname,
    string ControlPlaneUrl);