// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Plexor.Host — entry point for the Plexor control plane (REST + gRPC API).
// ============================================================================
// Wires:
//   - WebApplication.CreateBuilder + Microsoft.AspNetCore.OpenApi (source-gen)
//   - Health probes
//   - NodeAgent control loop endpoints (Register/Heartbeat/Poll/Result)
//   - DI for the in-memory node registry (singleton; state is
//     process-local; v0.2+ swaps to Postgres)
//
// AddOpenApi() registers the IDocumentProvider that the
// Microsoft.Extensions.ApiDescription.Server build target needs to emit
// artifacts/openapi.json at build-time (consumed by frontend codegen).
//
// Rule: top-level statements end with `app.Run()` (synchronous, returns
// void). NO async top-level statements — that would generate an implicit
// `<Main>$` returning Task, which violates VSTHRD200 (Async suffix rule).
// ============================================================================

using Plexor.Host.Abstractions;
using Plexor.Host.Controllers;
using Plexor.Host.NodeRegistry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Controllers — discovered from the application assembly.
// AddProblemDetails() (added later in composition) wires the
// global RFC 7807 error response for unhandled exceptions; the
// per-endpoint [ProducesResponseType<...>] attributes document
// the success paths.
builder.Services
    .AddControllers()
    .AddJsonOptions(static options =>
    {
        // Accept enums as JSON strings in any case ("succeeded",
        // "Succeeded", "SUCCEEDED" all bind to CommandResultStatus
        // .Succeeded). Required because the OpenAPI contract
        // describes status values in lowercase while the C# enum
        // uses PascalCase.
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .AddApplicationPart(typeof(NodeController).Assembly);

// NodeAgent control loop. Singleton state — restarting Plexor.Host
// forgets every node and its pending commands. v0.2+ moves to
// Postgres + a durable queue (NATS or Postgres LISTEN/NOTIFY).
builder.Services.AddSingleton<INodeRegistry, InMemoryNodeRegistry>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "plexor-host" }));
app.MapGet("/", () => Results.Ok(new { name = "Plexor Host", version = "0.1.0-dev" }));

app.MapControllers();

app.Run();
