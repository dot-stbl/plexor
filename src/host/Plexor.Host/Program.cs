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

using System.Text.Json.Serialization;
using Plexor.Host.Installers;
using Plexor.Host.OpenApi;
using Plexor.Modules.Clusters.Infrastructure.Installers;
using Plexor.Modules.Sigil.Api;
using Plexor.Modules.Sigil.Application.Installers;
using Plexor.Modules.Sigil.Infrastructure.Installers;
using Plexor.Shared.Filtering.DI;
using Plexor.Shared.Persistence;
using Plexor.Shared.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI — Microsoft.AspNetCore.OpenApi source-gen document provider.
// ProblemDetailsResponsesTransformer injects the standard RFC 7807
// error responses (400/401/403/404/409/500) into every operation so
// per-endpoint [ProducesResponseType] only has to document 2xx shapes.
builder.Services.AddOpenApi(static options => options.AddOperationTransformer<ProblemDetailsResponsesTransformer>());

// ProblemDetails baseline — every unhandled exception and every
// status-code page renders as application/problem+json. Combined with
// the OpenAPI transformer above, the document and the wire format stay
// in lock-step (no per-endpoint [ProducesResponseType<ProblemDetails>]
// required).
builder.Services.AddProblemDetails();

// Controllers — discovered from the application assembly.
// Per-endpoint [ProducesResponseType] only documents 2xx shapes;
// 4xx/5xx are wired centrally via ProblemDetailsResponsesTransformer
// and the AddProblemDetails() block above.
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
                new JsonStringEnumConverter());
        })
        .AddApplicationPart(typeof(Plexor.Modules.Sigil.Api.Controllers.AuthController).Assembly);

// Persistence — schema-per-module DbContexts. Connection string
// Persistence — single Postgres connection string, schema-per-module.
// All PlexorDbContext subclasses in Plexor.Modules.*.Infrastructure
// assemblies are discovered at startup and registered against the
// shared connection string (sigil / realm / atlas schemas all live
// in the same Postgres instance — schema-per-module is the isolation
// primitive, not database-per-module).
//
// The Migrator CLI applies pending migrations before Host starts in
// production; in dev you can run `dotnet ef database update` against
// the same string for any single context.
var postgresConnection = builder.Configuration.GetConnectionString("Postgres")
                         ?? throw new InvalidOperationException(
                             "ConnectionStrings:Postgres missing from configuration.");
var contextCount = builder.Services.AddPlexorModuleDbContexts(postgresConnection);

// Filterable entities — Plexor.Shared.Filtering registry. Each call to
// AddFilterableEntity<T> marks the entity's properties for the filter
// DSL: the OpenAPI schema transformer emits x-filterable + x-sortable on
// the matching schema, and the kubb plugin generates a typed filter
// builder per entity. No entity is registered yet — Sigil's User / Role
// list endpoints (Phase 4) will register here. The registry is wired so
// the transformer can run today; without it, every schema is non-
// filterable.
builder.Services.AddFiltering();

// Sigil module — auth contracts + impls. Phase 3.2-3.5 wires the
// PBKDF2 password hasher + the per-request ICurrentUser reader.
// The bearer handler that populates claims lands in Phase 3.6;
// until then ICurrentUser always returns the anonymous defaults
// (Guid.Empty ids + empty collections).
builder.Services.AddSigilApplicationCore(builder.Configuration);
builder.Services.AddSigilInfrastructureCore(builder.Configuration);
builder.Services.AddPlexorSigilApi();

// Clusters module — control-plane fleet (Cluster + Node aggregates +
// NodeAgent join/heartbeat endpoints). Phase 5.
builder.Services.AddClustersInfrastructureCore(builder.Configuration);
builder.Services.AddExceptionHandler<Plexor.Modules.Clusters.Infrastructure.Errors.ClustersExceptionHandler>();

// Strip our own IHostedService implementations when the host is being
// launched by the build-time OpenAPI document generator. Without this,
// `dotnet build` would run SigningKeyBootstrapper (and any other IHostedService
// that talks to Postgres) just to emit artifacts/openapi.json.
builder.Services.RemoveHostedServicesForOpenApiGeneration();

// Logging — Plexor console formatter (color-coded by level, formatted
// for grep-ability). Replaces the default simple formatter so all ASP.NET
// logs go through PlexorConsoleFormatter at startup. Must be called
// BEFORE builder.Build() because the logging service collection is
// frozen once the host is built.
builder.Logging.AddPlexorConsole();

var app = builder.Build();

app.Logger.LogInformation(
    "Registered {ContextCount} DbContext(s) against ConnectionStrings:Postgres",
    contextCount);

app.MapGet("/health", static () => Results.Ok(new { status = "ok", service = "plexor-host" }));
app.MapGet("/", static () => Results.Ok(new { name = "Plexor Host", version = "0.1.0-dev" }));

// ProblemDetails error pipeline:
//   UseExceptionHandler — unhandled exceptions → 500 application/problem+json
//   UseStatusCodePages  — 404/415/etc. without a body → application/problem+json
// Both rely on the IProblemDetailsService registered by AddProblemDetails().
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapControllers();

app.Run();
