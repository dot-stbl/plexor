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
using Plexor.Modules.Audit.Domain;
using Plexor.Modules.Audit.Infrastructure.Persistence;
using Plexor.Modules.Identity.Application.Abstractions;
using Plexor.Modules.Identity.Domain;
using Plexor.Modules.Identity.Domain.Entities;
using Plexor.Modules.Identity.Infrastructure.CurrentUser;
using Plexor.Modules.Identity.Infrastructure.Persistence;
using Plexor.Modules.Organizations.Infrastructure.Persistence;
using Plexor.Shared.Filtering;
using Plexor.Shared.Persistence;

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
// is read from ConnectionStrings:Audit in appsettings. The Migrator
// CLI applies migrations before Host starts in production; in dev
// you can run `dotnet ef database update` against the same string.
// v0.1: registered but no controller is wired yet — Phase 1 of the
// persistence migration lands the read paths next.
var auditConnection = builder.Configuration.GetConnectionString("Audit")
                      ?? throw new InvalidOperationException(
                          "ConnectionStrings:Audit missing from configuration.");
builder.Services.AddModuleDbContext<AuditDbContext>(auditConnection);

var tenantsConnection = builder.Configuration.GetConnectionString("Realm")
                        ?? throw new InvalidOperationException(
                            "ConnectionStrings:Realm missing from configuration.");
builder.Services.AddModuleDbContext<RealmDbContext>(tenantsConnection);

var identityConnection = builder.Configuration.GetConnectionString("Identity")
                         ?? throw new InvalidOperationException(
                             "ConnectionStrings:Identity missing from configuration.");
builder.Services.AddModuleDbContext<IdentityDbContext>(identityConnection);

// Filterable entities — Plexor.Shared.Filtering registry. Adding
// AuditEntry makes its properties available to the kubb plugin via
// x-filterable / x-sortable extensions on the Audit schema.
builder.Services.AddFiltering().AddFilterableEntity<AuditEntry>();

// Auth primitives — built-in PasswordHasher (PBKDF2) + per-request
// current user from HttpContext claims. The bearer handler that
// populates the claims is wired in Phase 3.6; until then the
// ICurrentUser reader always returns the anonymous defaults
// (Guid.Empty ids + empty collections).
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.PasswordHasher<User>>();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "plexor-host" }));
app.MapGet("/", () => Results.Ok(new { name = "Plexor Host", version = "0.1.0-dev" }));

app.MapControllers();

app.Run();
