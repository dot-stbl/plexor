// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Plexor.Host — entry point for the Plexor control plane (REST + gRPC API).
// ============================================================================
// Skeleton placeholder. The actual Program.cs will be wired with:
//   - WebApplication.CreateBuilder + OpenAPI + Scalar
//   - AuthN/AuthZ (Keycloak JWT bearer)
//   - Module DI composition (Plexor.Shared.Composition)
//   - Health checks + Prometheus exporter
//   - OpenTelemetry tracing/metrics
//
// AddOpenApi() registers the IDocumentProvider that the
// Microsoft.Extensions.ApiDescription.Server build target needs to emit
// artifacts/openapi.json at build-time (consumed by frontend codegen).
//
// Rule: top-level statements end with `app.Run()` (synchronous, returns
// void). NO async top-level statements — that would generate an implicit
// `<Main>$` returning Task, which violates VSTHRD200 (Async suffix rule).
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "plexor-host" }));
app.MapGet("/", () => Results.Ok(new { name = "Plexor Host", version = "0.1.0-dev" }));

app.Run();
