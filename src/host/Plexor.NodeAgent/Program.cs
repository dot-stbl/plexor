// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Plexor.NodeAgent — Worker Service entry point. Runs on every compute node,
// receives tasks from Plexor.Host via gRPC/mTLS, dispatches to local providers.
// ============================================================================
// Skeleton placeholder. Real implementation:
//   - HostApplicationBuilder + OpenTelemetry + Serilog
//   - gRPC client to Plexor.Host with mTLS
//   - Long-running IHostedService that streams tasks
//   - Local provider health checks
//
// Rule: end with `app.Run()`. See async-and-tasks.md §3 and VSTHRD200.
// ============================================================================

using Plexor.NodeAgent;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<NodeAgentWorker>();

var app = builder.Build();
app.Run();