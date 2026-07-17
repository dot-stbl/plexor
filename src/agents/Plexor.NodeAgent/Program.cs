// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Plexor.NodeAgent — Worker Service entry point. Runs on every compute node,
// pulls commands from Plexor.Host over HTTP, dispatches to local
// providers (libvirt/KVM in v0.1), reports results back.
//
// DI composition:
//   - ICommandTransport -> HttpCommandTransport (named HttpClient)
//   - IWorkloadProvider -> LibvirtKvmProvider (only provider in v0.1)
//   - IWorkloadRegistry -> InMemoryWorkloadRegistry
//   - ICommandExecutor   -> WorkloadCreateExecutor + WorkloadActionExecutor
//   - CommandDispatcher  (singleton; built from the executor list)
//   - NodeAgentWorker    (hosted service; owns the join/heartbeat/poll
//                          loop and dispatches incoming commands)
//
// Rule: end with `app.Run()`. See async-and-tasks.md §3 and VSTHRD200.
// ============================================================================

using Plexor.NodeAgent;
using Plexor.NodeAgent.Abstractions;
using Plexor.NodeAgent.Composition;
using Plexor.NodeAgent.Executors;
using Plexor.NodeAgent.Infrastructure;
using Plexor.NodeAgent.Providers;
using Plexor.NodeAgent.Providers.Image;
using Plexor.NodeAgent.Providers.Network;
using Plexor.NodeAgent.Providers.Storage;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Workloads;
using Refit;

var builder = Host.CreateApplicationBuilder(args);

// ----------------------------------------------------------------------------
// mTLS options — the cert triple the join flow writes to disk
// (under <c>~/.plexor/agent/</c>) is loaded by the SocketsHttpHandler
// below. Enrolled flag flips true on successful join, so the
// pre-join HTTP call to /join still uses a plain HttpClient
// (the host endpoint is anonymous) and only after enrollment do
// we substitute the mTLS handler.
// ----------------------------------------------------------------------------
var certDirectoryOverride = builder.Configuration["NodeAgent:Mtls:CertDirectory"];
var nodeOptions = new NodeAgentOptions
{
    CertDirectory = !string.IsNullOrEmpty(certDirectoryOverride)
        ? certDirectoryOverride
        : System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".plexor", "agent"),
};
builder.Services.AddSingleton(nodeOptions);

// HTTP transport to Plexor.Host. The Refit-typed client is
// registered first; the HttpCommandTransport is the concrete
// ICommandTransport that calls into it. The resilience handler
// applies per-typed-client (retry + circuit breaker + per-attempt
// timeout) — three retries on transient failures before the
// agent gives up on a poll cycle and logs the failure.
//
// BaseAddress is the control-plane root plus the API version
// segment (Plexor.Shared.Contracts.Routes.ApiRoutes.Base = api/v1).
// The transport's relative paths ('nodes/join', 'nodes/{id}/
// heartbeat', etc.) don't include the segment, so the BaseAddress
// has to.
//
// Primary handler: SocketsHttpHandler with mTLS client cert +
// host-cert validation against the Plexor CA. Only installed
// once the agent has completed the join flow — pre-join calls
// (the /join itself) use a plain handler because the host
// endpoint is anonymous.
builder.Services
        .AddRefitClient<INodeApi>(NodeApiSettingsFactory.Create())
        .ConfigureHttpClient(client =>
        {
            var baseAddress = builder.Configuration["Plexor:ControlPlaneUrl"]
                              ?? "http://localhost:5000/";

            var apiBase = baseAddress.TrimEnd('/') + "/" +
                          ApiRoutes.Base;

            client.BaseAddress = new Uri(apiBase);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
            nodeOptions.Enrolled
                ? (HttpMessageHandler)MtlsHttpHandlerFactory.Build(nodeOptions)
                : new SocketsHttpHandler())
        .AddStandardResilienceHandler();

builder.Services.AddSingleton<ICommandTransport, HttpCommandTransport>();

// Compute backends — one per storage / image / network
// technology. v0.1 ships LocalDirStorage + LocalDirImageRegistry
// + LinuxBridgeBackend. Multi-node deployments register
// additional backends alongside (e.g. CephRbdStorage) — the
// workload provider picks the right one via DI.
//
// Image registry options (mutually exclusive — call only one;
// both register the same IImageRegistry singleton, the second
// call overwrites the first):
//   - AddLocalDirImageRegistry: operator pre-populates a
//     directory with base images, registry returns the cached
//     path on every call. Offline-friendly (default for v0.1).
//   - AddHttpImageRegistry:      registry downloads from a
//     configured URL on first request, caches to
//     /var/lib/plexor/images-cache. Production default once
//     a signed image registry exists.
builder.Services
        .AddLocalDirImageRegistry(builder.Configuration)
        .AddLocalDirStorage(builder.Configuration)
        .AddLinuxBridgeNetwork();
// .AddHttpImageRegistry(builder.Configuration); // uncomment to
// override LocalDir

// Workload providers. v0.1: KVM, LXC, and QEMU-software-emulation
// via libvirt (three technologies, three different WorkloadKinds).
// The InMemoryWorkloadRegistry ctor takes an
// IEnumerable<IWorkloadProvider> — DI auto-injects every
// registered provider here. Add more providers as the project
// grows (k3s, podman).
//
// All three providers consume the compute backends above
// (volume + network). KVM uses VolumeFormat.Qcow2 + LinuxBridge;
// LXC uses VolumeFormat.Directory (bind-mount rootfs) +
// LinuxBridge; QEMU uses VolumeFormat.Qcow2 + LinuxBridge. The
// IWorkloadProvider is interface-agnostic — it only knows the
// format + kind, the backend implementations do the rest.
builder.Services.AddSingleton<LibvirtKvmProvider>();
builder.Services.AddSingleton<LibvirtLxcProvider>();
builder.Services.AddSingleton<LibvirtQemuProvider>();
builder.Services.AddSingleton<IWorkloadProvider>(sp => sp.GetRequiredService<LibvirtKvmProvider>());
builder.Services.AddSingleton<IWorkloadProvider>(sp => sp.GetRequiredService<LibvirtLxcProvider>());
builder.Services.AddSingleton<IWorkloadProvider>(sp => sp.GetRequiredService<LibvirtQemuProvider>());
builder.Services.AddSingleton<IWorkloadRegistry, InMemoryWorkloadRegistry>();

// Workload executors — one per wire command type. The
// dispatcher is built from the registered ICommandExecutor
// list at first use (singleton).
builder.Services.AddSingleton<ICommandExecutor, WorkloadCreateExecutor>();
builder.Services.AddSingleton<ICommandExecutor, WorkloadActionExecutor>();
builder.Services.AddSingleton<CommandDispatcher>();

// The worker is the BackgroundService that owns the
// join/heartbeat/poll/submit loop. v0.1 reads node hardware + the
// control-plane URL from configuration; v0.2+ moves to a
// typed IOptions<NodeConfig> with validation.
builder.Services.AddSingleton(new NodeAgentWorker.NodeConfig(
    builder.Configuration.GetValue("Plexor:Node:CpuCores", Environment.ProcessorCount),
    builder.Configuration.GetValue("Plexor:Node:RamBytes", 8L * 1024 * 1024 * 1024),
    builder.Configuration.GetValue("Plexor:Node:DiskBytes", 100L * 1024 * 1024 * 1024),
    builder.Configuration.GetValue<string>("Plexor:Node:Hostname")
    ?? Environment.MachineName,
    builder.Configuration["Plexor:ControlPlaneUrl"]
    ?? "http://localhost:5000/"));

builder.Services.AddHostedService<NodeAgentWorker>();

var app = builder.Build();
app.Run();
