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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging.Abstractions;
using Plexor.Host.Filters;
using Plexor.Host.Installers;
using Plexor.Host.Models;
using Plexor.Host.NodeAgent;
using Plexor.Host.OpenApi;
using Plexor.Host.Validation;
using Plexor.Modules.Audit.Api.Endpoints;
using Plexor.Modules.Audit.Api.Installers;
using Plexor.Modules.Audit.Application.Audit;
using Plexor.Modules.Audit.Application.Installers;
using Plexor.Modules.Audit.Domain.Entities;
using Plexor.Modules.Audit.Infrastructure.Installers;
using Plexor.Modules.Audit.Infrastructure.Persistence;
using Plexor.Modules.Branding.Api;
using Plexor.Modules.Branding.Api.Endpoints;
using Plexor.Modules.Branding.Api.Installers;
using Plexor.Modules.Branding.Application.Installers;
using Plexor.Modules.Branding.Infrastructure.Installers;
using Plexor.Modules.Branding.Infrastructure.Persistence;
using Plexor.Modules.Clusters.Infrastructure.Installers;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Quotas.Api.Errors;
using Plexor.Modules.Quotas.Api.Installers;
using Plexor.Modules.Quotas.Application.Installers;
using Plexor.Modules.Quotas.Infrastructure.Installers;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Modules.Realm.Infrastructure.AuthProviders;
using Plexor.Modules.Realm.Infrastructure.Persistence;
using Plexor.Modules.Sigil.Api;
using Plexor.Modules.Sigil.Api.Endpoints;
using Plexor.Modules.Sigil.Application.Installers;
using Plexor.Modules.Sigil.Infrastructure.Installers;
using Plexor.Modules.Sigil.Infrastructure.Persistence;
using Plexor.Modules.Network.Api.Endpoints;
using Plexor.Modules.Network.Api.Installers;
using Plexor.Modules.Network.Application.Installers;
using Plexor.Modules.Network.Infrastructure.Installers;
using Plexor.Modules.Network.Infrastructure.Persistence;
using Plexor.Modules.Storage.Api.Endpoints;
using Plexor.Modules.Storage.Api.Installers;
using Plexor.Modules.Storage.Application.Installers;
using Plexor.Modules.Storage.Infrastructure.Installers;
using Plexor.Modules.Storage.Infrastructure.Persistence;
using Plexor.Shared.Configuration;
using Plexor.Shared.Filtering.DI;
using Plexor.Shared.Mtls;
using Plexor.Shared.Mtls.Persistence;
using Plexor.Shared.Persistence;
using Plexor.Shared.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------------------------------
// Plexor config stack — layered on top of the default JSON sources
// WebApplication.CreateBuilder wired up (appsettings.json +
// appsettings.{Environment}.json). Adds:
//   1. TOML at PlexorConfigPaths.DefaultConfigFile() (the OS-conventional
//      user-level config file). Missing file → no-op.
//   2. PLX_* environment variables — highest priority.
// PLX_CONFIG_FILE overrides the user-level path entirely; useful for
// tests and CI.
// ----------------------------------------------------------------------------
builder.Configuration.AddPlexorConfiguration();

// ----------------------------------------------------------------------------
// mTLS bootstrap. CA root + host server cert are file-based and
// must exist BEFORE ConfigureKestrel captures the PFX path. We do this
// synchronously here, idempotently — first boot generates; subsequent
// boots reuse the same files. Logger is a simple console one — the
// Plexor-formatted logger isn't wired yet at this point in the
// composition root, and bootstrap only emits 1-2 lines.
// ----------------------------------------------------------------------------
var caOptions = builder.Configuration
    .GetSection(CertAuthorityOptions.SectionName)
    .Get<CertAuthorityOptions>()
    ?? new CertAuthorityOptions();
PlexorCaBootstrap.EnsureCertificates(
    caOptions,
    NullLogger.Instance);

builder.Services.AddPlexorCertAuthority(builder.Configuration);
builder.Services.AddHostedService<PlexorCaStartup>();

// ----------------------------------------------------------------------------
// Data Protection (4.6.1) — encrypts the OIDC client secret at rest in
// realm.org_auth_provider_configs.oidc_client_secret_protected. The
// keyring lives on disk under the OS-conventional per-user data
// directory (PlexorPaths.DefaultDataRoot); the host-only purpose
// string is namespaced so the OIDC secret cannot be decrypted by a
// purpose-bound protector minted elsewhere. The same keyring is
// shared by every host in a multi-pod deployment only when the
// data root is mounted as a shared volume — for self-host (single
// pod) the default location is the right shape.
// ----------------------------------------------------------------------------
var dataProtectionDir = Path.Combine(
    PlexorPaths.DefaultDataRoot(),
    "dataprotection-keys");
Directory.CreateDirectory(dataProtectionDir);
builder.Services
    .AddDataProtection(static options => options.ApplicationDiscriminator = "plexor-host")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionDir));

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
        .AddApplicationPart(typeof(Plexor.Modules.Sigil.Api.Controllers.AuthController).Assembly)
        // 4.5.g.2 — QuotasController (GET /api/v1/quotas/*) lives in
        // the Quotas.Api assembly; AddApplicationPart makes it
        // discoverable alongside the Sigil controllers above.
        .AddApplicationPart(typeof(Plexor.Modules.Quotas.Api.Controllers.QuotasController).Assembly)
        // BrandingController (GET /api/v1/branding/*) lives in the
        // Branding.Api assembly; AddApplicationPart makes it
        // discoverable alongside the Quotas controllers above.
        // ThemeInstallationsController (GET/PUT/DELETE
        // /api/v1/branding/theme) lives in the same assembly and is
        // picked up by the same AddApplicationPart call.
        .AddApplicationPart(typeof(Plexor.Modules.Branding.Api.Controllers.BrandingController).Assembly);

// Persistence — single shared NpgsqlDataSource + schema-per-module DbContexts.
// All PlexorDbContext subclasses in Plexor.Modules.*.Infrastructure assemblies
// are registered against the same connection pool, so cross-DbContext
// transactions work (the 4.5.c quota enforcer runs its UPDATE on
// quotas.quota_usage inside the resource-create transaction opened on
// ClusterDbContext; both writes commit atomically because they share a
// physical connection).
//
// The Migrator CLI applies pending migrations before Host starts in
// production; in dev you can run `dotnet ef database update` against
// the same string for any single context.
var postgresConnection = builder.Configuration.GetConnectionString("Postgres")
                         ?? throw new InvalidOperationException(
                             "ConnectionStrings:Postgres missing from configuration.");

// Build the shared data source once at the top of the composition
// root and pass it explicitly to every AddModuleDbContext call.
// AddPlexorDataSource registers the instance as a singleton so any
// other service that needs the data source can resolve it.
var plexorDataSource = builder.Services.AddPlexorDataSource(postgresConnection);

// Explicit DbContext registration — same set + order as the migrator.
builder.Services.AddModuleDbContext<RealmDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<IdentityDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<ClusterDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<RevokedCertsDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<QuotasDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<BrandingDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<AuditDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<StorageDbContext>(plexorDataSource);
builder.Services.AddModuleDbContext<NetworkDbContext>(plexorDataSource);
builder.Services.AddScoped<IAuditDbContext>(sp => sp.GetRequiredService<AuditDbContext>());
var contextCount = 9;

// Filterable entities — Plexor.Shared.Filtering registry. Each call to
// AddFilterableEntity<T> marks the entity's properties for the filter
// DSL: the OpenAPI schema transformer emits x-filterable + x-sortable on
// the matching schema, and the kubb plugin generates a typed filter
// builder per entity. Phase 5.2 registers AuditEntry — the
// GET /api/v1/audit endpoint is the first consumer; future 5.3 admin
// UI endpoints (filter by org + action + actor) reuse the same
// registration. Sigil's User / Role list endpoints (Phase 4) register
// here in a follow-up.
builder.Services.AddFiltering()
    .AddFilterableEntity<AuditEntry>();

// ----------------------------------------------------------------------------
// Realm auth-providers (4.6.1) — wired BEFORE Sigil infrastructure.
// Sigil.Infrastructure.OidcTokenClient + ExternalOidcAuthProvider
// resolve IOrgAuthProviderConfigReader at request time, so the seam
// has to be in the container before AddSigilInfrastructureCore runs.
// The runtime only requires per-request resolution (DI builds the
// graph lazily), but registering in dependency order keeps the
// ValidateOnBuild / scope-validation paths honest and matches the
// architecture rules (Realm → Sigil).
builder.Services.AddRealmAuthProviders();

// Sigil module — auth contracts + impls. Phase 3.2-3.5 wires the
// PBKDF2 password hasher + the per-request ICurrentUser reader.
// The bearer handler that populates claims lands in Phase 3.6;
// until then ICurrentUser always returns the anonymous defaults
// (Guid.Empty ids + empty collections).
builder.Services.AddSigilApplicationCore(builder.Configuration);
builder.Services.AddSigilInfrastructureCore();
builder.Services.AddPlexorSigilApi();

// Clusters module — control-plane fleet (Cluster + Node aggregates +
// NodeAgent join/heartbeat endpoints). Phase 5.
builder.Services.AddClustersInfrastructureCore();
builder.Services.AddExceptionHandler<Plexor.Modules.Clusters.Infrastructure.Errors.ClustersExceptionHandler>();

// Quotas module — Phase 4.5.b ships the enforcer + scope resolver +
// catalog reader; 4.5.c wires the enforcer into Compute resource-create
// paths; 4.5.d into Storage + Network; 4.5.e adds IRateLimiter +
// RateLimitFilter + the cleanup BackgroundService; 4.5.g adds the
// controllers. Application layer has no services today (the catalog
// seed is hosted by Plexor.Migrator); the call still goes through
// AddQuotasApplicationCore so the Program.cs chain stays stable as
// Application services land.
builder.Services.AddQuotasApplicationCore(builder.Configuration);
builder.Services.AddQuotasInfrastructureCore();
// QuotasApi (4.5.g.3) — registers the FluentValidation validator for
// the PUT /api/v1/quotas/assignments body. Controllers are still
// discovered via AddApplicationPart above; this installer only adds
// Api-layer DI registrations. Mirrors AddPlexorSigilApi for the
// Sigil module.
builder.Services.AddQuotasApiCore();

// Realm auth-providers (4.6.1) — purpose-bound IDataProtector for
// the OIDC client secret. The controller resolves the
// purpose-scoped protector via IDataProtectionProvider.CreateProtector
// at the call site so each action carries the right purpose.
builder.Services.AddScoped<IValidator<UpsertOrgAuthProviderRequest>, UpsertOrgAuthProviderRequestValidator>();
// Named HttpClient for the OIDC discovery-document fetch. A
// separate client keeps the default request policy (no auth, no
// retries) out of the controller's discovery fetch.
builder.Services.AddHttpClient("Plexor-OidcDiscovery", static client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Plexor-Host/0.1");
});
// QuotaExceptionHandler (4.5.g.1) — maps QuotaExceededException
// thrown by resource-create handlers (4.5.c Compute.CreateCluster /
// CreateWorkload, 4.5.d Storage / Network) to an HTTP 429
// ProblemDetails. Without this handler the exception would bubble up
// as a 500 via the global error pipeline — misleading to a caller
// that hit a capacity wall. Sits next to IdentityExceptionHandler +
// ClustersExceptionHandler; 4.5.g.2 adds the QuotasController.
builder.Services.AddExceptionHandler<QuotaExceptionHandler>();

// Branding module — operator-global + per-org branding.
// Application installs the BrandingGlobalSeederHostedService; the
// Infrastructure installer wires the EF-backed IBrandingService;
// the API installer registers the FluentValidation validators.
// Mirrors the Quotas trio above (Application + Infrastructure + Api).
builder.Services.AddBrandingApplicationCore(builder.Configuration);
builder.Services.AddBrandingInfrastructureCore();
builder.Services.AddBrandingApiCore();
// Bind BrandingOptions so the /custom.css endpoint knows where to
// find the operator's escape-hatch CSS file. Same pattern as the
// CertAuthorityOptions binding for the CA bootstrap.
builder.Services
    .AddOptions<BrandingOptions>()
    .Bind(builder.Configuration.GetSection(BrandingOptions.SectionName));

// Audit module (Phase 5.1) — emits generic IAuditEmitter events
// from quota + future auth-provider controllers. Application layer
// is empty in 5.1 (the audit read endpoint lands in 5.2); the
// Infrastructure installer wires the EF-backed DbAuditEmitter.
// Mirrors the Quotas/Branding Application + Infrastructure pair.
// 5.2 — AuditApiInstaller adds the empty wiring point for the
// GET /api/v1/audit endpoint (no per-request DI surface yet, but
// the slot stays so the Program.cs chain stays stable as 5.3
// retention options land).
builder.Services.AddAuditApplicationCore(builder.Configuration);
builder.Services.AddAuditInfrastructureCore();
builder.Services.AddAuditApiCore();

// Storage module (Phase 4.5.d) — volumes + buckets in the `storage`
// schema. The Application + Infrastructure installers wire the
// IStorageQuotaReader seam the Quotas enforcer needs to read the
// current org-scoped volume count + cumulative GiB. The Api installer
// (commit 2) registers the FluentValidation validators for the
// POST endpoints; the MapStorageEndpoints call below mounts the
// minimal-API routes alongside the controllers.
builder.Services.AddStorageApplicationCore(builder.Configuration);
builder.Services.AddStorageInfrastructureCore();
builder.Services.AddStorageApiCore();

// Network module (Phase 4.5.d) — floating IPs + load balancers in
// the `network` schema. The Application + Infrastructure installers
// wire the INetworkQuotaReader seam the Quotas enforcer needs for
// network.floating_ips.count + network.load_balancers.count. The
// Api installer (commit 4) registers the FluentValidation
// validators for the POST endpoints; the MapNetworkEndpoints call
// below mounts the minimal-API routes alongside the controllers.
builder.Services.AddNetworkApplicationCore(builder.Configuration);
builder.Services.AddNetworkInfrastructureCore();
builder.Services.AddNetworkApiCore();
// Audit retention (Phase 5.3) — bind AuditOptions so the daily
// sweep BackgroundService picks up RetentionDays / CleanupInterval /
// BatchSize / SweepHourUtc. ValidateDataAnnotations + ValidateOnStart
// fail the host startup on an out-of-range value rather than the
// first sweep. Mirrors the BrandingOptions binding above.
builder.Services
    .AddOptions<AuditOptions>()
    .Bind(builder.Configuration.GetSection(AuditOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// OrgSeederHostedService (4.5.f) needs a way to enumerate the org ids
// to seed. The Quotas module does not depend on Realm — we supply the
// seam from here so the Quotas layer stays loosely coupled. Singleton
// delegate; the inner factory opens a fresh scope on each call so the
// scoped RealmDbContext lifetime is respected. Non-async lambda body
// — the Realm query is a simple SELECT id, so sync ToList is fine;
// wrapping in Task.FromResult avoids the Task<List<T>> → Task<IReadOnlyCollection<T>>
// invariance issue that trips the async-lambda form.
builder.Services.AddSingleton<Func<CancellationToken, Task<IReadOnlyCollection<Guid>>>>(
    static sp => async cancellationToken =>
    {
        await using var scope = sp.CreateAsyncScope();
        var realm = scope.ServiceProvider.GetRequiredService<RealmDbContext>();
        var ids = realm.Organizations
            .Select(static organization => organization.Id)
            .ToList();
        return await Task.FromResult<IReadOnlyCollection<Guid>>(ids);
    });

// Rate-limit action filter (4.5.e) — runs on every authenticated
// action before the controller. Anonymous requests bypass the filter
// (auth middleware emits 401 before this point). The filter is
// registered globally via MvcOptions.Filters so per-controller
// allowlists are not yet wired (Phase 2 concern).
builder.Services.Configure<MvcOptions>(static options => options.Filters.Add<RateLimitFilter>());

// Strip our own IHostedService implementations when the host is being
// launched by the build-time OpenAPI document generator. Without this,
// `dotnet build` would run SigningKeyBootstrapper (and any other IHostedService
// that talks to Postgres) just to emit artifacts/openapi.json.
builder.Services.RemoveHostedServicesForOpenApiGeneration();

// TimeProvider — single source of wall-clock for the host. Quotas
// enforcer reads it for quota_usage.last_reconciled_at / updated_at;
// future modules (Compute retry budgets, audit timestamps) use the
// same injected clock.
builder.Services.AddSingleton(TimeProvider.System);

// Logging — Plexor console formatter (color-coded by level, formatted
// for grep-ability). Replaces the default simple formatter so all ASP.NET
// logs go through PlexorConsoleFormatter at startup. Must be called
// BEFORE builder.Build() because the logging service collection is
// frozen once the host is built.
builder.Logging.AddPlexorConsole();

// ----------------------------------------------------------------------------
// Kestrel — dual endpoint. :48001 is browser-facing HTTP (JWT bearer
// from the Sigil stack). :48002 is NodeAgent-facing mTLS — every
// incoming request must present a client cert signed by the Plexor
// CA. The MtlsAuthMiddleware (registered below, after routing)
// validates the chain, checks revocation, and parses CN into NodeId.
// ----------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(48001);
    options.ListenAnyIP(48002, listenOptions =>
    {
        listenOptions.UseHttps(httpsOptions =>
        {
            httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            // Load cert + key as PEM — the format PlexorCaBootstrap
            // writes. PEM is supported by every .NET SDK version via
            // X509CertificateLoader.LoadCertificate + RSA.ImportFromPem,
            // no deprecated-constructor warnings.
            var cert = X509CertificateLoader.LoadCertificate(
                File.ReadAllBytes(caOptions.HostCertPath));
            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(caOptions.HostKeyPath));
            httpsOptions.ServerCertificate = cert.CopyWithPrivateKey(rsa);
        });
    });
});

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

// mTLS auth middleware — runs before controllers. Allows requests
// through if their path is browser-facing; rejects (401) if the
// NodeAgent-facing path lacks a valid Plexor-CA-signed client cert.
app.UseMiddleware<MtlsAuthMiddleware>();

app.MapControllers();

// Storage module endpoints (Phase 4.5.d) — minimal-API surface for
// volumes + buckets. The endpoints are NOT controller-discovered
// (the .AddApplicationPart chain above only finds MVC controllers);
// they're mapped explicitly here alongside the other minimal-API
// endpoints (custom.css, OIDC flow, audit query).
app.MapStorageEndpoints();

// Network module endpoints (Phase 4.5.d) — minimal-API surface for
// floating IPs + load balancers. Same minimal-API mapping pattern
// as StorageEndpoints above.
app.MapNetworkEndpoints();

// Custom CSS endpoint — serves the operator's custom.css escape
// hatch (commit 5). Mounted before MapControllers so it takes
// priority over any controller route with the same path.
app.MapCustomCssEndpoint();

// OIDC flow endpoints (Phase 4.6.3b) — the inbound anonymous
// surface for tenants configured with an external OIDC IDP.
//   /auth/oidc/authorize  → redirect to the IDP's auth endpoint
//   /auth/oidc/callback   → finish the PKCE flow + IDP code exchange
//   /auth/oidc/logout     → RP-initiated logout (best-effort)
// Mounted before MapControllers so they take priority over any
// future controller route with the same path.
app.MapOidcAuthorize();
app.MapOidcCallback();
app.MapOidcLogout();

// Audit query endpoint (Phase 5.2) — GET /api/v1/audit. Tenant-
// scoped, paginated read surface for the append-only audit log;
// backed by the IAuditEmitter writes that DbAuditEmitter +
// OrgAuthProvidersController produce. The endpoint is a minimal
// API (MapGet, not a controller) and is mapped explicitly here
// rather than discovered via AddApplicationPart. Mounted after
// MapControllers so the controllers' generic fall-through routes
// (catch-all 404 handlers, etc.) take priority on collision.
app.MapAuditQuery();

app.Run();
