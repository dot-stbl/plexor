# Specification + Filtering — full integration example

Walk-through of how `Plexor.Shared.Persistence` (Specification pattern via
`Ardalis.Specification.EntityFrameworkCore`) composes with
`Plexor.Shared.Filtering` (DSL `FilterQuery`) in a controller. End shape:
`HTTP /api/v1/audit?filter=...&sort=...&page=...&pageSize=...` →
`PageResult<AuditSummary>`.

The reference is the Audit module (`Plexor.Modules.Audit`); the same shape
applies to any module that needs paged, filterable, sortable list endpoints.

---

## Layered view

```
Controller        — API surface + binding
   ↓ (FilterQuery)
Query Service     — DbContext + Specification<T, TResult> + filter DSL
   ↓ (IQueryable<TEntity>)
Specification     — query criteria (immutable, composable)
   ↓ (IQueryable<TResult>)
Projection        — filtered .Select(t => new AuditSummary(...))
   ↓
ToPagedResultAsync — single roundtrip: count + slice
```

`Plexor.Shared.Filtering` adds `ApplyFilter` / `ApplySort` against the
**entity** queryable (`IQueryable<TEntity>`); the projection runs after sort
and applies to the projected queryable. Order matters: filter and sort on
the **entity**, then project, then paginate.

---

## 1. Entity (Domain layer)

```csharp
// src/modules/Plexor.Modules.Audit.Domain/AuditEntry.cs
using Plexor.Modules.Audit.Domain.Events;

namespace Plexor.Modules.Audit.Domain;

/// <summary>
/// One row in the audit log: who did what, against which resource, when.
/// Append-only — updates are forbidden.
/// </summary>
public sealed class AuditEntry
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid ActorId { get; init; }
    public string Action { get; init; } = string.Empty;        // "vm.create", "vm.delete"
    public string ResourceType { get; init; } = string.Empty;  // "vm", "node"
    public Guid? ResourceId { get; init; }
    public string Outcome { get; init; } = string.Empty;        // "succeeded" | "failed"
    public DateTimeOffset OccurredAt { get; init; }
}
```

`AuditEntry` is an entity record (immutable init properties). Append-only
operations live in `AuditEntryCommands` (write-side); reads live in the
query service below.

---

## 2. Projections (Application layer)

```csharp
// src/modules/Plexor.Modules.Audit.Application/Models/AuditSummary.cs
namespace Plexor.Modules.Audit.Application.Models;

/// <summary>List-row projection. 7 fields, no navigation.</summary>
public sealed record AuditSummary(
    Guid Id,
    Guid ActorId,
    string Action,
    string ResourceType,
    Guid? ResourceId,
    string Outcome,
    DateTimeOffset OccurredAt);

// src/modules/Plexor.Modules.Audit.Application/Models/AuditDetail.cs

/// <summary>Single-item projection. Includes the optional correlation id.</summary>
public sealed record AuditDetail(
    Guid Id,
    Guid TenantId,
    Guid ActorId,
    string Action,
    string ResourceType,
    Guid? ResourceId,
    Guid? CorrelationId,
    string Outcome,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string> Metadata);
```

Two role-specific names (no generic `Dto` suffix). `Summary` for list rows,
`Detail` for single-item view. The projection lives in `Application/Models/`
because `Application/Models/` is the canonical DTO location
(see `coding/anti-patterns.md` §records-DTO-placement).

---

## 3. Filterable fields (Domain — registered per-entity)

```csharp
// src/modules/Plexor.Modules.Audit.Domain/Filterable/AuditEntryFieldSet.cs
using Plexor.Shared.Filtering;

namespace Plexor.Modules.Audit.Domain.Filterable;

/// <summary>
/// Per-entity registry of fields the filter DSL can target. Built once
/// via reflection; reused on every request. New entity property -> new
/// filterable field automatically (opt-out: [NotMapped] excludes).
/// </summary>
public static class AuditEntryFieldSet
{
    public static readonly FilterableFieldSet<AuditEntry> Instance =
        FilterableFieldRegistry.For<AuditEntry>();
}
```

Every public instance property of `AuditEntry` is filterable unless tagged
`[NotMapped]`. `FilterOperator` set is inferred from the CLR type
(string -> `~`/`==`/`!=`, DateTimeOffset -> `>=`/`<`/`>`, nullable Guid ->
also `?`/`!?`, etc.).

---

## 4. Specification (Application layer)

```csharp
// src/modules/Plexor.Modules.Audit.Application/Specifications/AuditByTenantSpec.cs
using Ardalis.Specification;
using Plexor.Modules.Audit.Application.Models;
using Plexor.Modules.Audit.Domain;

namespace Plexor.Modules.Audit.Application.Specifications;

/// <summary>
/// "All audit entries visible to tenant X" — base spec, composable
/// via <see cref="WithActor" />, <see cref="WithResource" />, etc.
/// Projects to <see cref="AuditSummary" /> (TResult != T — 2 type params).
/// </summary>
public sealed class AuditByTenantSpec : Specification<AuditEntry, AuditSummary>
{
    public AuditByTenantSpec(Guid tenantId)
    {
        Query
            .Where(entry => entry.TenantId == tenantId)
            .OrderByDescending(entry => entry.OccurredAt)
            .Select(entry => new AuditSummary(
                entry.Id,
                entry.ActorId,
                entry.Action,
                entry.ResourceType,
                entry.ResourceId,
                entry.Outcome,
                entry.OccurredAt));
    }

    /// <summary>Opt-in narrower: only entries by this actor.</summary>
    public AuditByTenantSpec WithActor(Guid actorId)
    {
        Query.Where(entry => entry.ActorId == actorId);
        return this;
    }

    /// <summary>Opt-in narrower: only entries for this resource type.</summary>
    public AuditByTenantSpec WithResourceType(string resourceType)
    {
        Query.Where(entry => entry.ResourceType == resourceType);
        return this;
    }

    /// <summary>Opt-in narrower: only entries with this outcome.</summary>
    public AuditByTenantSpec WithOutcome(string outcome)
    {
        Query.Where(entry => entry.Outcome == outcome);
        return this;
    }
}
```

Two type params: `<AuditEntry, AuditSummary>`. The `Query.Select(...)` is
the projection — the spec materialises `IQueryable<AuditSummary>` directly,
not `IQueryable<AuditEntry>`. **Filter / sort happen on the entity query**
inside the spec; the projection runs once at the end.

`WithActor` / `WithResourceType` / `WithOutcome` are **fluent narrowers**
(not filters) — they extend `Query` and `return this`, so `var spec = new
AuditByTenantSpec(tenantId).WithActor(actorId);` composes naturally.

---

## 5. Query service (Application layer — DbContext + spec)

```csharp
// src/modules/Plexor.Modules.Audit.Application/QueryService/AuditQueryService.cs
using Ardalis.Specification.EntityFrameworkCore;
using Plexor.Modules.Audit.Application.Models;
using Plexor.Modules.Audit.Application.Specifications;
using Plexor.Modules.Audit.Domain.Filterable;
using Plexor.Shared.Contracts.Pagination;
using Plexor.Shared.Filtering;

namespace Plexor.Modules.Audit.Application.QueryService;

public sealed class AuditQueryService(AuditDbContext db)
{
    /// <summary>
    /// List audit entries for a tenant, with filter DSL + sort DSL + paging.
    /// Single roundtrip: COUNT + slice via <c>ToPagedResultAsync</c>.
    /// </summary>
    public async Task<PageResult<AuditSummary>> ListAsync(
        Guid tenantId,
        FilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var normalized = query.Normalized();

        // 1. Start with base spec (tenant scope + default order + projection)
        var spec = new AuditByTenantSpec(tenantId);

        // 2. Apply DSL filter on the entity query, before the projection runs.
        //    ApplyFilter expects FilterableFieldSet<T> for the source type —
        //    here T = AuditEntry. The spec already has .Select(...) projection,
        //    but filter/sort run on the underlying IQueryable<AuditEntry>
        //    via the EF interceptor chain.
        var filtered = db.AuditEntries
            .ApplyFilter(normalized.Filter, AuditEntryFieldSet.Instance)
            .ApplySort(normalized.Sort, AuditEntryFieldSet.Instance);

        // 3. Re-spec the filtered query against the projection.
        //    Specification's ApplySpecification builds the projected query.
        var projected = SpecificationEvaluator.Default.GetQuery(
            filtered, spec);

        // 4. Paginate.
        var total = await projected.CountAsync(cancellationToken);
        var items = await projected
            .Skip(normalized.Skip())
            .Take(normalized.PageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<AuditSummary>(
            items,
            total,
            normalized.Page,
            normalized.PageSize);
    }

    public Task<AuditDetail?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Simple fetch — no spec needed; single-entity read.
        return db.AuditEntries
            .Where(entry => entry.TenantId == tenantId && entry.Id == id)
            .Select(entry => new AuditDetail(
                entry.Id,
                entry.TenantId,
                entry.ActorId,
                entry.Action,
                entry.ResourceType,
                entry.ResourceId,
                entry.CorrelationId,
                entry.Outcome,
                entry.OccurredAt,
                entry.Metadata.ToDictionary(p => p.Key, p => p.Value)))
            .FirstOrDefaultAsync(cancellationToken)!;
    }
}
```

`Plexor.Shared.Filtering.ApplyFilter` / `ApplySort` consume the **entity
queryable** (before projection). The spec adds the **projection** after that
— `SpecificationEvaluator.GetQuery` glues `ApplyFilter`→entity→spec's
`Select`→`AuditSummary`→paging into one roundtrip.

> ⚠️ The order is `ApplyFilter/ApplySort` first, **then** spec projection.
> If you skip the spec's `.Select(...)` (using
> `Specification<AuditEntry, AuditEntry>` identity), filtering and sort
> still work — but no projection.

---

## 6. Controller (API layer)

```csharp
// src/modules/Plexor.Modules.Audit.Infrastructure/Controllers/AuditController.cs
using Microsoft.AspNetCore.Authorization;
using Plexor.Modules.Audit.Application.Models;
using Plexor.Modules.Audit.Application.QueryService;
using Plexor.Shared.Contracts.Pagination;
using Plexor.Shared.Contracts.Routes;
using Plexor.Shared.Filtering;

namespace Plexor.Modules.Audit.Infrastructure.Controllers;

[ApiController]
[Route($"{ApiRoutes.Base}/audit")]
[Authorize]
public sealed class AuditController(
    AuditQueryService query,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>List audit entries (filter + sort + page).</summary>
    [HttpGet(Name = "audit-list")]
    [EndpointSummary("List audit entries for the current tenant")]
    [ProducesResponseType<PageResult<AuditSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PageResult<AuditSummary>>> ListAsync(
        [FromQuery] FilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.TenantId;
        var result = await queryService.ListAsync(tenantId, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Single audit entry by id.</summary>
    [HttpGet("{entryId:guid}", Name = "audit-get-by-id")]
    [EndpointSummary("Get a single audit entry by id")]
    [ProducesResponseType<AuditDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditDetail>> GetByIdAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        return await queryService.GetByIdAsync(currentUser.TenantId, entryId, cancellationToken)
            is { } entry
                ? Ok(entry)
                : NotFound();
    }
}
```

`FilterQuery` binds `?filter=...&sort=...&page=...&pageSize=...` (4 query
params). The controller is the **only** place HTTP-aware types
(`[FromQuery]`, `ActionResult<>`) appear — the service returns
`PageResult<AuditSummary>`, no `IActionResult`.

The 200 response type is **mandatory** (per `coding/api-design.md` §3).
4xx/5xx come from `builder.Services.AddProblemDetails()` globally.

---

## 7. DbContext (Infrastructure — snake_case + HasMaxLength)

```csharp
// src/modules/Plexor.Modules.Audit.Infrastructure/Persistence/AuditDbContext.cs
using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Audit.Domain;

namespace Plexor.Modules.Audit.Infrastructure.Persistence;

[ConnectionString($"{AuditDbContext.SectionConstants.DatabaseSection}:Audit")]
public sealed class AuditDbContext(
    DbContextOptions<AuditDbContext> options) : PlexorDbContext(options)
{
    public const string SectionConstants = "ConnectionStrings";  // root config namespace

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Audit);

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable(DatabaseInformation.Tables.AuditEntries);

            entity.HasKey(static entry => entry.Id);

            entity.Property(static entry => entry.Id)
                .HasColumnName("id")
                .HasColumnType("char(36)")
                .IsRequired();

            entity.Property(static entry => entry.TenantId)
                .HasColumnName("tenant_id")
                .HasColumnType("char(36)")
                .IsRequired();

            entity.Property(static entry => entry.ActorId)
                .HasColumnName("actor_id")
                .HasColumnType("char(36)")
                .IsRequired();

            entity.Property(static entry => entry.Action)
                .HasColumnName("action")
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(static entry => entry.ResourceType)
                .HasColumnName("resource_type")
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(static entry => entry.ResourceId)
                .HasColumnName("resource_id")
                .HasColumnType("char(36)");

            entity.Property(static entry => entry.CorrelationId)
                .HasColumnName("correlation_id")
                .HasColumnType("char(36)");

            entity.Property(static entry => entry.Outcome)
                .HasColumnName("outcome")
                .HasMaxLength(16)
                .IsRequired();

            entity.Property(static entry => entry.OccurredAt)
                .HasColumnName("occurred_at");

            entity.Property(static entry => entry.Metadata)
                .HasColumnName("metadata")
                .HasColumnType("jsonb");
        });
    }
}
```

Every string column has `HasMaxLength(n)`. Every Guid stored as `char(36)`
(value-object conversion lives elsewhere). Single-line DTO-bindings via
`HasColumnName("snake_case")`. Schema is `DatabaseInformation.Schemes.Audit`
(= `atlas` in our architecture theme per `.agents/STATE.md`).

---

## 8. Composition root (Host Program.cs)

```csharp
// Add DbContext (snake_case + schema-per-module)
builder.Services.AddModuleDbContext<AuditDbContext>(connectionString);

// Register Application services (one installer per module)
builder.Services.AddSingleton<AuditQueryService>();
```

`AddModuleDbContext<T>(string)` lives in `Plexor.Shared.Persistence` —
it applies `.UseSnakeCaseNamingConvention()` automatically.

---

## Decision tree — when to use what

| Situation | What |
|-----------|------|
| Single-entity read (`db.Set<X>().FirstOrDefaultAsync(...)`) | LINQ, no spec needed |
| Trivial where (`db.Set<X>().Where(...).ToListAsync()`) | LINQ, no spec needed |
| Multi-filter, sorted, paged, projected | **Spec + Filter** (this doc) |
| Bulk operation (Update/Delete) | LINQ `ExecuteUpdate/ExecuteDelete` |
| Cross-aggregate composition (e.g. join + project to union DTO) | Hand-written query, **not spec** |

---

## When NOT to use a Specification

- **No projection needed** and **no composable filters** — a single
  `Where(...).FirstOrDefaultAsync()` is fine, don't wrap in spec.
- **Cross-aggregate compositing** — specs model *one* aggregate root;
  for `JOIN` across two roots, write a hand query and project.
- **Stored procedures / `FromSqlRaw`** — wrap in a thin repository, no spec.
- **Bulk update / delete** — `ExecuteUpdate` / `ExecuteDelete` (no reads).

---

## Related

- `architecture/persistence.md` — Specification pattern, schema-per-module, anti-`IRepository<T>`
- `architecture/traffic.md` — REST endpoints + Refit clients (audit service client lives here)
- `coding/api-design.md` — controller skeleton (`[ProducesResponseType]`, `[EndpointSummary]`)
- `coding/anti-patterns.md` §records-DTO-placement — `Summary`/`Detail` naming
- `Plexor.Shared.Filtering` DSL grammar — `FilterQuery.cs` XML doc
- `Plexor.Shared.Persistence.Specification` — base class (Ardalis)
