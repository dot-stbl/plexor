---
description: plexor ef core / dapper / specification pattern. Без generic IRepository<T> обёрток. Specification для сложных queries, filtered projections в query, DbContext per module со schema-per-module.
globs: ["**/*.cs"]
always: true
---

# Persistence layer (EF Core + Dapper + Specification)

## Stack

- **EF Core 10** для writes + DDD entities/migrations
- **Dapper** для read-heavy paths (audit query, billing analytics, bulk reads)
- **PostgreSQL** единственная БД, schema-per-module (см. `STATE.md`)
- **One DbContext per module** в `*.Infrastructure/Persistence/`
- **NATS** для cross-module events (post-v0.1, не v0.1)

## Правило: NO generic IRepository<T>

**EF Core `DbSet<T>` уже Repository + UnitOfWork.** Обёртка `INodeRepository.Add/Update/Delete/GetById` — это anti-DRY, anti-CQRS, без value-add:

```csharp
// ❌ ANTI-PATTERN — никогда так
public interface INodeRepository
{
    Task<NodeRecord?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<NodeRecord>> ListAsync(CancellationToken ct);
    Task AddAsync(NodeRecord node, CancellationToken ct);
    // ... 12 more methods, each wrapping EF Core 1:1
}

public sealed class NodeRepository : INodeRepository
{
    public Task<NodeRecord?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Nodes.FirstOrDefaultAsync(n => n.Id == id, ct);  // 1:1 wrap
}
```

**Почему плохо:**
- `INodeRepository<T>.GetById` vs `db.Nodes.Find(id)` — одинаково
- Multi-repo = нет единой транзакции (`repoA.Save()` + `repoB.Save()` не атомарны)
- Generic `IRepository<T>` всегда leak'ит — `Include`, `ThenInclude`, `AsNoTracking`, `SplitQuery` специфичны для EF
- +1 слой indirection без value

## Application service — DbContext directly

```csharp
// ✅ OK
public sealed class TenantQueryService(TenantsDbContext db)
{
    public async Task<IReadOnlyList<TenantDto>> ListAsync(
        Specification<TenantRecord> spec, CancellationToken ct)
    {
        return await spec
            .Apply(db.Tenants.AsNoTracking())
            .Select(t => new TenantDto(t.Id, t.Name, t.CreatedAt))
            .ToListAsync(ct);
    }
}
```

DbContext инжектится в Application service. Application service **не** вызывает repository — он вызывает методы, которые семантически описывают use-case (ListAsync, GetByIdAsync).

## Specification pattern — для сложных queries

```csharp
// src/shared/Plexor.Shared.Persistence/Specification.cs
public abstract class Specification<T> where T : class
{
    /// <summary>Apply this spec's filter/order/include to the
    /// incoming IQueryable. Stateless, composable.</summary>
    public abstract IQueryable<T> Apply(IQueryable<T> query);
    
    public Specification<T> Include(Func<IQueryable<T>, IIncludableQueryable<T, object>> include);
    public Specification<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector);
    public Specification<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector);
    public Specification<T> Skip(int n);
    public Specification<T> Take(int n);
}
```

**Где НЕ нужен Specification:**
- Single-entity fetch (`db.Nodes.Find(id)`)
- Trivial query (`.Where(...).ToListAsync()`)
- 1-off query, не будет переиспользоваться

**Где Specification помогает:**
- Multi-filter queries ("active tenants, optionally filtered by name, role, date range, paged")
- Encapsulates query logic away from controller
- Reused across multiple call sites

## Filtered projections — push `Select` в query

```csharp
// ❌ Load full entity, then map
var entities = await db.Nodes.AsNoTracking().ToListAsync(ct);
return entities.Select(n => new NodeDto(n.Id, n.Hostname, n.State)).ToList();

// ✅ Projection в query
return await db.Nodes.AsNoTracking()
    .Select(n => new NodeDto(n.Id, n.Hostname, n.State))
    .ToListAsync(ct);
```

**Why:** EF Core транслирует `Select` в SQL `SELECT id, hostname, state`. Не грузим `cpu_cores, ram_bytes, disk_bytes, hostname, kernel_version, ...` если DTO нужны только 3 поля. **AsNoTracking** для read-only queries — большой perf win (не материализуем entity в change-tracker).

## DbContext per module

```csharp
// src/modules/Plexor.Modules.Identity.Infrastructure/Persistence/IdentityDbContext.cs
public sealed class IdentityDbContext : PlexorDbContext
{
    public DbSet<UserRecord> Users => Set<UserRecord>();
    public DbSet<RoleRecord> Roles => Set<RoleRecord>();
    public DbSet<RefreshTokenRecord> RefreshTokens => Set<RefreshTokenRecord>();
    
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("sigil");  // schema-per-module, см. STATE.md
        // Configure entities, relationships, indexes...
    }
}
```

`PlexorDbContext` — base в `Plexor.Shared.Persistence`:
```csharp
public abstract class PlexorDbContext : DbContext
{
    /// <summary>Override to set schema name (per-module convention).
    /// Convention: schema = Architecture theme word (sigil/realm/...).
    /// See STATE.md "Schema-per-module".</summary>
    protected abstract void OnModelCreating(ModelBuilder mb);
    
    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);
        // Add common conventions: snake_case, soft delete, audit fields
    }
}
```

## Где Repository ОК (aggregate root для write-стороны)

## Filter + sort + paging DSL — `Plexor.Shared.Filtering`

Не пишем свой DSL-парсер. **Используем `Plexor.Shared.Filtering`** (мигрирован из
console.x — см. `git log -- src/shared/Plexor.Shared.Filtering/`):

- `FilterQuery` envelope с `[FromQuery]` bindings (`Filter`, `Sort`, `Page`, `PageSize`)
- DSL: `name~John;status==Active` (AND), `(a|b)` (OR), `now(-7d)`, `[]=Active,Trial` (IN)
- Sort: `name,asc;createdAt,desc` (multi-criteria, OrderBy + ThenBy)
- `QueryableFilterExtensions.ApplyFilter/ApplySort` — extension на `IQueryable<T>`
- `FilterableFieldSet<TEntity>` — per-entity field registry с reflection

**Пример controller'а:**

```csharp
[HttpGet("")]
public async Task<ActionResult<PageResult<UserDto>>> ListAsync(
    [FromQuery] FilterQuery query,
    CancellationToken ct = default)
{
    var fields = UserFieldSet.Instance;  // per-entity, registered at startup
    var spec = Specification.Default<UserRecord, UserDto>(u => new UserDto(u.Id, u.Email))
        .AsNoTracking();
    
    var filtered = db.Users
        .ApplyFilter(query.Filter, fields)
        .ApplySort(query.Sort, fields);
    
    var paged = await filtered.ToPagedResultAsync(query.Page, query.PageSize, ct);
    return Ok(paged);
}
```

Где `ToPagedResultAsync` — extension из `Plexor.Shared.Persistence` (wraps count + skip/take). Под капотом — `IQueryable.CountAsync()` + `.Skip().Take()`. Реальная paging-библиотека не нужна — spec разбирает DSL, query делает EF.

## Где Repository ОК (aggregate root для write-стороны)

**DDD aggregate repository** — не generic `IRepository<T>`, а специфичный для aggregate root:

```csharp
// ✅ OK: aggregate repository для write-стороны
public sealed class TenantAggregateRepository(PlexorDbContext db)
{
    /// <summary>Create tenant + first project + admin membership
    /// in a single transaction. Business invariant: a tenant
    /// MUST have an admin user.</summary>
    public async Task CreateAsync(
        TenantRecord tenant, ProjectRecord firstProject, MembershipRecord admin, CancellationToken ct)
    {
        db.Tenants.Add(tenant);
        db.Projects.Add(firstProject);
        db.Memberships.Add(admin);
        await db.SaveChangesAsync(ct);  // single transaction
    }
}
```

Это **не generic** — метод `CreateAsync` специфичен для aggregate root (Tenant + first project + admin user — business invariant). Содержит knowledge о структуре агрегата, не просто `Add`.

**v0.1:** aggregate repositories пишем только если модуль реально нуждается в multi-entity транзакциях с инвариантами. Single-entity writes идут через DbContext directly.

## Self-audit

```bash
# Generic IRepository<T> — должно быть пусто
rg -n "interface I[A-Z][a-zA-Z]+Repository" src/ --type cs

# AsNoTracking в read-only paths
rg -n "AsNoTracking" src/ --type cs

# Specification usage
rg -n "Specification<" src/ --type cs

# Schema-per-module naming
rg -n "HasDefaultSchema\(" src/ --type cs
# Должно быть: "sigil", "realm", "ledger", "atlas", "forge", "outpost", "shard"
```

## Anti-patterns

- ❌ `interface IRepository<T>` — generic, leak'ит EF specifics, +1 indirection
- ❌ Load full entity then project to DTO — `AsNoTracking().Select(...)` лучше
- ❌ Multiple `SaveChangesAsync` calls in different "repositories" — один DbContext = одна транзакция
- ❌ `Specification<T>` для trivial queries (1 line) — overkill
- ❌ `Specification<T>` для write-стороны — DbContext.SaveChanges() + aggregate repository
- ❌ `ToList()` без проекции — load'ит все columns, performance hit
- ❌ Skip migrations в dev — `Add-Migration` обязателен, schema-per-module — никаких "manual" SQL
