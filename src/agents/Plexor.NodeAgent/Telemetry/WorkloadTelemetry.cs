// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadTelemetry — OpenTelemetry instruments for the
// NodeAgent's workload lifecycle. One ActivitySource +
// one Meter per NodeAgent assembly; both named after the
// assembly so the host's telemetry registration can wire
// them into the OTLP exporter with `AddSource(...)` +
// `AddMeter(...)`.
//
// What's instrumented:
//   - Span "Plexor.NodeAgent.Workload.create" — around the
//     full LibvirtKvmProvider.CreateAsync. Tags:
//       workload.kind        ("vm" / "lxc" / "qemu")
//       workload.name        (libvirt domain name)
//       workload.base_image   (ref of the base image, when known)
//       workload.image_size_bytes
//   - Span "Plexor.NodeAgent.Workload.start" — around
//     StartAsync. Tags: workload.kind, workload.name.
//   - Histogram
//     "plexor.nodeagent.workload.create.duration" (unit: s) —
//     every CreateAsync contributes one observation. Buckets
//     cover the realistic range (50ms — 5min) for a cold
//     image clone + virsh define + virsh start.
//
// Why file-static + named after the assembly:
//   - Plexor.Shared.Telemetry already references OpenTelemetry
//     packages; we reuse those transitively rather than
//     adding a new package.
//   - ActivitySource + Meter are static by design; one per
//     process. Per-class instances would create duplicate
//     spans / meters on the host.
//   - The Meter is registered with `AddMeter(...)` on the
//     host's OpenTelemetry pipeline (see Plexor.Host /
//     Plexor.NodeAgent's Program.cs); without that wiring
//     the instruments are no-ops (matches diagnostics.md
//     convention).
// ==========================================================================

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Plexor.NodeAgent.Telemetry;

/// <summary>
///     Telemetry primitives for the NodeAgent's workload
///     lifecycle. Single source / meter per assembly.
/// </summary>
public static class WorkloadTelemetry
{
    /// <summary>
    ///     ActivitySource for workload-lifecycle spans
    ///     (<c>Plexor.NodeAgent.Workload</c>). Host
    ///     registrations: <c>services.AddOpenTelemetry()
    ///     .WithTracing(t =&gt; t.AddSource(WorkloadTelemetry.SourceName))</c>.
    /// </summary>
    public const string SourceName = "Plexor.NodeAgent.Workload";

    /// <summary>
    ///     Meter name for workload-lifecycle metrics. Host
    ///     registrations: <c>services.AddOpenTelemetry()
    ///     .WithMetrics(m =&gt; m.AddMeter(WorkloadTelemetry.MeterName))</c>.
    /// </summary>
    public const string MeterName = "Plexor.NodeAgent.Workload";

    /// <summary>
    ///     ActivitySource instance. Re-used across the
    ///     NodeAgent assembly — one per process.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(SourceName);

    /// <summary>
    ///     Meter instance. Re-used across the NodeAgent
    ///     assembly — one per process.
    /// </summary>
    public static readonly Meter Meter = new(MeterName);

    /// <summary>
    ///     End-to-end latency of <c>IWorkloadProvider.CreateAsync</c>,
    ///     in seconds. Buckets chosen for the realistic range:
    ///     - 50ms — in-memory clone from a warm cache
    ///     - 500ms — qemu-img create + virsh define
    ///     - 30s — full cloud image pull + clone + start
    ///     - 5min — pathological / cold-cache worst case
    /// </summary>
    public static readonly Histogram<double> CreateDuration =
        Meter.CreateHistogram<double>(
            name: "plexor.nodeagent.workload.create.duration",
            unit: "s",
            description: "Wall-clock latency of IWorkloadProvider.CreateAsync, in seconds.");
}
