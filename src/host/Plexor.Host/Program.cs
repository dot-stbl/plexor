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
using Plexor.Host.Abstractions;
using Plexor.Host.Controllers;
using Plexor.Host.NodeRegistry;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Domain;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.CurrentUser;
using Plexor.Shared.Filtering;
using Plexor.Shared.Persistence;
using Plexor.Shared.Telemetry;

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
                new JsonStringEnumConverter());
        })
        .AddApplicationPart(typeof(NodeController).Assembly);

// NodeAgent control loop. Singleton state — restarting Plexor.Host
// forgets every node and its pending commands. v0.2+ moves to
// Postgres + a durable queue (NATS or Postgres LISTEN/NOTIFY).
builder.Services.AddSingleton<INodeRegistry, InMemoryNodeRegistry>();

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

// Auth primitives — built-in PasswordHasher (PBKDF2) + per-request
// current user from HttpContext claims. The bearer handler that
// populates the claims is wired in Phase 3.6; until then the
// ICurrentUser reader always returns the anonymous defaults
// (Guid.Empty ids + empty collections).
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.PasswordHasher<User>>();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

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

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "plexor-host" }));
app.MapGet("/", () => Results.Ok(new { name = "Plexor Host", version = "0.1.0-dev" }));

app.MapControllers();

app.Run();
