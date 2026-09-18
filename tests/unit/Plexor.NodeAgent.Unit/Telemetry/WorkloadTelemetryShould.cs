// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadTelemetry unit tests — verify that the NodeAgent's
// OpenTelemetry instruments emit as expected when LibvirtKvmProvider
// runs CreateAsync / StartAsync.
//
// Strategy:
//   - Subscribe an ActivityListener to the source name before
//     invoking the provider; assert the listener received the
//     span with the expected name + tags.
//   - Subscribe a MeterListener to the meter name; assert it
//     received the histogram observation (the listener fires
//     once per Record call).
//   - Both listeners are disposed at the end of each test so
//     they don't leak across tests (Activity / Meter listeners
//     are process-wide; without dispose, an unrelated test
//     could see them).
//
// The tests don't depend on OpenTelemetry's exporter pipeline
// — the System.Diagnostics built-in listener hooks capture
// everything before the OTLP SDK even sees it.
// ==========================================================================

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Plexor.NodeAgent.Providers;
using Plexor.NodeAgent.Providers.Image;
using Plexor.NodeAgent.Providers.Network;
using Plexor.NodeAgent.Providers.Storage;
using Plexor.NodeAgent.Telemetry;
using Plexor.Shared.Compute;
using Plexor.Shared.NodeApi;
using Shouldly;
using Xunit;

namespace Plexor.NodeAgent.Unit.Telemetry;

public sealed class WorkloadTelemetryShould
{
    [Fact(DisplayName = "Given a CreateAsync call, when the provider runs, then a 'Plexor.NodeAgent.Workload.create' span is started with workload.kind + workload.name tags")]
    public async Task CreateSpanEmittedWithExpectedTagsAsync()
    {
        var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WorkloadTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { /* no-op; the listener just needs to exist */ }
        };
        ActivitySource.AddActivityListener(activityListener);

var captured = new List<Activity>();
        var capturingListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WorkloadTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => captured.Add(activity)
        };
        ActivitySource.AddActivityListener(capturingListener);

        try
        {
            var (sut, _, _, _) = NewKvmProvider(out _);
            // Use a unique workload name so we can filter out
            // activities from concurrent tests that share the
            // process-wide ActivityListener registration.
            var uniqueName = $"vm-telemetry-create-{Guid.NewGuid():N}".Substring(0, 40);
            var spec = NewSpec(uniqueName, """
                {
                  "Vcpu": 1,
                  "RamBytes": 268435456,
                  "BaseImageRef": null
                }
                """);

            try
            {
                await sut.CreateAsync(spec, CancellationToken.None);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Expected on Windows / hosts without virsh —
                // the span emission happens before the failure,
                // so we still have the assertion evidence.
            }

            // Filter by name to ignore concurrent tests'
            // activities (xUnit runs test classes in parallel by
            // default; ActivitySource listeners are process-wide).
            var createSpans = captured
                .Where(a => a.OperationName == "Plexor.NodeAgent.Workload.create")
                .Where(a => (string?)a.GetTagItem("workload.name") == uniqueName)
                .ToList();
            createSpans.ShouldNotBeEmpty(
                $"CreateAsync should emit a span named 'Plexor.NodeAgent.Workload.create' for {uniqueName}.");

            var span = createSpans[0];
            span.GetTagItem("workload.kind").ShouldBe("vm");
            span.GetTagItem("workload.name").ShouldBe(uniqueName);
        }
        finally
        {
            capturingListener.Dispose();
            activityListener.Dispose();
        }
    }

[Fact(DisplayName = "Given a StartAsync call, when the provider runs, then a 'Plexor.NodeAgent.Workload.start' span is started")]
    public async Task StartSpanEmittedAsync()
    {
        // Pre-register a workload so StartAsync can find it.
        var (sut, _, _, _) = NewKvmProvider(out _);
        // Unique name filters cross-class listener pollution.
        var uniqueName = $"vm-telemetry-start-{Guid.NewGuid():N}".Substring(0, 40);
        var createSpec = NewSpec(uniqueName, """
            { "Vcpu": 1, "RamBytes": 268435456, "BaseImageRef": null }
            """);

        Guid workloadId;
        try
        {
            var workload = await sut.CreateAsync(createSpec, CancellationToken.None);
            workloadId = workload.Id;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Can't test StartAsync without a registered
            // workload; skip on hosts without virsh.
            return;
        }

        var captured = new List<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WorkloadTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => captured.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        try
        {
            try
            {
                await sut.StartAsync(workloadId, CancellationToken.None);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Expected on Windows / hosts without virsh.
            }

            // Filter by name to ignore concurrent tests'
            // activities (xUnit runs test classes in parallel
            // by default; ActivitySource listeners are
            // process-wide).
            var startSpans = captured
                .Where(a => a.OperationName == "Plexor.NodeAgent.Workload.start")
                .Where(a => (string?)a.GetTagItem("workload.name") == uniqueName)
                .ToList();
            startSpans.ShouldNotBeEmpty(
                $"StartAsync should emit a span named 'Plexor.NodeAgent.Workload.start' for {uniqueName}.");

            var span = startSpans[0];
            span.GetTagItem("workload.kind").ShouldBe("vm");
            span.GetTagItem("workload.name").ShouldBe(uniqueName);
        }
        finally
        {
            listener.Dispose();
        }
    }

[Fact(DisplayName = "Given a CreateAsync call, when the provider runs, then the plexor.nodeagent.workload.create.duration histogram records an observation")]
    public async Task CreateDurationHistogramRecordsObservationAsync()
    {
        // MeterListener must be Started (and thus subscribed
        // to measurements) BEFORE the histogram first records.
        // We use a MeterListener that publishes all instruments
        // it sees and counts each double measurement.
        var counter = 0;
        var meterName = WorkloadTelemetry.MeterName;
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
        {
            Interlocked.Increment(ref counter);
        });
        listener.Start();

        var (sut, _, _, _) = NewKvmProvider(out _);
        var spec = NewSpec("vm-telemetry-histogram", """
            { "Vcpu": 1, "RamBytes": 268435456, "BaseImageRef": null }
            """);

        try
        {
            await sut.CreateAsync(spec, CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Expected on Windows / hosts without virsh —
            // the histogram still records.
        }

        counter.ShouldBeGreaterThan(0,
            "CreateAsync should record at least one observation on plexor.nodeagent.workload.create.duration.");

        listener.Dispose();
    }

    private static (LibvirtKvmProvider Sut, IVolumeBackend Volumes, INetworkBackend Networks, TimeProvider Clock) NewKvmProvider(
        out List<VolumeSpec> volumeCalls)
    {
        var volumeStore = new List<VolumeSpec>();
        var volumes = Substitute.For<IVolumeBackend>();
        volumes.CreateAsync(Arg.Do<VolumeSpec>(s => volumeStore.Add(s)), Arg.Any<CancellationToken>())
            .Returns(new VolumeHandle(LocalDirStorage.BackendName, "/var/lib/plexor/volumes/test.qcow2"));

        var networks = Substitute.For<INetworkBackend>();
        networks.AttachAsync(Arg.Any<NetworkSpec>(), Arg.Any<CancellationToken>())
            .Returns(new NetworkInterfaceHandle(LinuxBridgeBackend.BackendName, "br-default"));

        volumeCalls = volumeStore;

        var provider = new LibvirtKvmProvider(
            volumes,
            networks,
            NullLogger<LibvirtKvmProvider>.Instance,
            TimeProvider.System);

        return (provider, volumes, networks, TimeProvider.System);
    }

    private static WorkloadSpec NewSpec(string name, string configJson)
    {
        var element = JsonSerializer.Deserialize<JsonElement>(configJson);
        return new WorkloadSpec(new WorkloadKind.Vm(), name, element);
    }
}
