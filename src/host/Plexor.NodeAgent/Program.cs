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
using Plexor.Shared.Workloads;
using Refit;

var builder = Host.CreateApplicationBuilder(args);

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
builder.Services
    .AddRefitClient<INodeApi>(NodeApiSettingsFactory.Create())
    .ConfigureHttpClient(client =>
    {
        var baseAddress = builder.Configuration["Plexor:ControlPlaneUrl"]
            ?? "http://localhost:5000/";
        var apiBase = baseAddress.TrimEnd('/') + "/" +
            Plexor.Shared.Contracts.Routes.ApiRoutes.Base;
        client.BaseAddress = new Uri(apiBase);
    })
    .AddStandardResilienceHandler();

builder.Services.AddSingleton<ICommandTransport, HttpCommandTransport>();

// Workload providers. v0.1: only KVM via libvirt. Add more
// providers here as the project grows (LXC, k3s, podman).
builder.Services.AddSingleton<LibvirtKvmProvider>();
builder.Services.AddSingleton<IWorkloadRegistry>(sp =>
    new InMemoryWorkloadRegistry());
builder.Services.AddSingleton<IWorkloadProvider>(sp => sp.GetRequiredService<LibvirtKvmProvider>());

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
    CpuCores: builder.Configuration.GetValue<int>("Plexor:Node:CpuCores", Environment.ProcessorCount),
    RamBytes: builder.Configuration.GetValue<long>("Plexor:Node:RamBytes", 8L * 1024 * 1024 * 1024),
    DiskBytes: builder.Configuration.GetValue<long>("Plexor:Node:DiskBytes", 100L * 1024 * 1024 * 1024),
    Hostname: builder.Configuration.GetValue<string>("Plexor:Node:Hostname")
        ?? Environment.MachineName,
    ControlPlaneUrl: builder.Configuration["Plexor:ControlPlaneUrl"]
        ?? "http://localhost:5000/"));
builder.Services.AddHostedService<NodeAgentWorker>();

var app = builder.Build();
app.Run();
