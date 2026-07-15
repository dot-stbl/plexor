---
description: plexor ef core / specification / ardalis-style repository pattern. Repository<T> + Specification<T, TResult> — primary read surface; DbContext — writes + aggregates. PageAsync = spec + filter DSL + paging.
globs: ["**/*.cs"]
always: true
---

# Persistence layer (EF Core + Specification + Repository<T>)

## Stack

- **EF Core 10** для writes + DDD entities/migrations
- **PostgreSQL** единственная БД, schema-per-module (см. `STATE.md`)
- **One DbContext per module** в `*.Infrastructure/Persistence/`
- **NATS** для cross-module events (post-v0.1, не v0.1)

## Access pattern — Repository<T> for reads, DbContext for writes

Plexor uses a **hybrid** access pattern: read endpoints go through
`Repository<T>` + `Specification<T, TResult>` (Ardalis-compatible), write
endpoints go through `DbContext` directly. The boundary is intentional —
read surface is repetitive boilerplate (filter/order/page/projection) that
belongs in a single seam, write surface has per-aggregate semantics that
don't.

### Read paths — Repository<T> + Specification<T, TResult>

```csharp
// src/shared/Plexor.Shared.Persistence/Repository.cs
public abstract class Repository<T> where T : class
{
    /// <summary>Apply spec to the typed DbSet and materialize.</summary>
    /// <returns>Tracked entities (default) or no-tracking (when spec has AsNoTracking()).</returns>
    public virtual Task<IEnumerable<T>> ListAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default);

    /// <summary>Projection variant — spec carries the Select(...).</summary>
    public virtual Task<IEnumerable<TResult>> ListAsync<TResult>(
        ISpecification<T, TResult> specification,
        CancellationToken cancellationToken = default);

    /// <summary>Count with the same predicate the spec carries.</summary>
    public virtual Task<int> CountAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default);

    /// <summary>Single entity by primary key.</summary>
    public virtual Task<T?> GetByIdAsync<TId>(
        TId id,
        CancellationToken cancellationToken = default);

    /// <summary>First match (or null). Spec carries the predicate.</summary>
    public virtual Task<T?> FirstOrDefaultAsync(
        ISpecification<T> specification,
        CancellationToken cancellationToken = default);
}

// Per-module subclass — the only place that knows the DbContext type:
public sealed class ClusterRepository(ClusterDbContext db) : Repository<Cluster>
{ /* defaults delegate to db.Clusters; customize only if needed */ }
```

Handler usage — `Spec` bundles filter + order + projection + tracking:

```csharp
// ✅ Read endpoint
public sealed class ListClustersQueryHandler(
    IRepository<Cluster> repo,
    ILogger<ListClustersQueryHandler> logger)
    : ICommandHandler<ListClustersQuery, ClusterPage>
{
    public async Task<ClusterPage> HandleAsync(ListClustersQuery q, CancellationToken ct)
    {
        var spec = new ClustersByOrgSpec(q.OrgId).Paginate(q.Page, q.PageSize);
        var items = await repo.ListAsync(spec, ct);
        var total = await repo.CountAsync(spec, ct);
        return new ClusterPage(items.ToList(), total, q.Page, q.PageSize);
    }
}
```

### PageAsync — Spec + Plexor.Shared.Filtering DSL combo

For endpoints with a `FilterQuery` URL binding, there's a one-liner on
`Repository<T>` that combines the spec's filter/order with the
`FilterableFieldSet`'s DSL parser, paginates, and returns
`PageResult<TResult>`:

```csharp
// Signature on Repository<T>:
public virtual async Task<PageResult<TResult>> PageAsync<TResult>(
    ISpecification<T, TResult> spec,
    FilterQuery query,
    FilterableFieldSet<T> fields,
    CancellationToken cancellationToken = default)
{
    var filtered = ApplyFilter(spec.Apply(_db.Set<T>().AsNoTracking()), query.Filter, fields);
    var sorted   = ApplySort(filtered, query.Sort, fields);
    var total    = await sorted.CountAsync(cancellationToken);
    var items    = await sorted
        .Skip((query.Page - 1) * query.PageSize)
        .Take(query.PageSize)
        .ToListAsync(cancellationToken);
    return new PageResult<TResult>(items, total, query.Page, query.PageSize);
}

// Controller — one call, no Where/Skip/Take boilerplate:
[HttpGet("")]
public async Task<ActionResult<PageResult<ClusterSummary>>> ListAsync(
    [FromQuery] FilterQuery query,
    [FromServices] IRepository<Cluster> repo,
    [FromServices] ClusterFieldSet fields,
    CancellationToken ct = default)
{
    var spec = new ClusterSummariesSpec();
    return Ok(await repo.PageAsync(spec, query, fields, ct));
}
```

`PageAsync` enforces **one roundtrip per call** (count + slice) — same
pattern as `ToPagedResultAsync` but applies the Filter DSL first so the
count is over the *filtered* set, not the whole table.

### Write paths — DbContext directly (always)

```csharp
// ✅ Write endpoint — multi-entity aggregate in one transaction
public sealed class CreateClusterCommandHandler(ClusterDbContext db)
    : ICommandHandler<CreateClusterCommand, JoinTokenResult>
{
    public async Task<JoinTokenResult> HandleAsync(CreateClusterCommand c, CancellationToken ct)
    {
        var cluster = new Cluster { ... };
        var token   = new JoinToken { ... };

        await db.Clusters.AddAsync(cluster, ct);
        await db.JoinTokens.AddAsync(token, ct);
        await db.SaveChangesAsync(ct);  // single transaction for the aggregate

        return new JoinTokenResult(cluster.Id, tokenSecret, token.ExpiresAt, cluster.Endpoint);
    }
}
```

**Why writes stay on DbContext:**
- Aggregate invariants span multiple tables (`Cluster + JoinToken` in one
  transaction) — a generic `IRepository<T>` per entity can't enforce
  that.
- Delete cascades, soft-delete, status transitions are entity-specific.
- AddAsync + SaveChangesAsync per logical operation = clean transaction
  boundaries.

**Where Repository IS still anti-pattern:**
- A `Repository<T>` that does `db.Set<T>().Find(id) → return entity` is a
  1:1 wrapper around EF — anti-DRY. Use `GetByIdAsync<TId>` on
  `Repository<T>` ONLY.
- A `Repository<T>.ListAsync()` with no spec parameter (just returns "all
  rows") — same anti-pattern. Always pass a spec.

### Anti-patterns

- ❌ `interface IRepository<T>` as a public-facing contract — keep `Repository<T>`
  as an abstract base class (subclasses can be mocked via NSubstitute for
  testing). The `IRepository<T>` interface leaks EF specifics (LINQ `IQueryable`)
  and forces every consumer to know about EF.
- ❌ Custom `Repository<T>` subclass that wraps each method 1:1 — extend the
  base; don't re-implement.
- ❌ `Repository<T>.ListAsync(spec)` returning `Task<IQueryable<T>>` — the whole
  point of the pattern is to materialize inside the seam. If the caller
  needs more LINQ composition, they should compose it *inside* the spec.
- ❌ Generic `Repository<T>.AddAsync/UPDATE/DELETE` — every `SaveChangesAsync`
  should happen in the handler so the transaction boundary is explicit.
- ❌ Specification without a spec class — inline lambdas in handlers leak
  the same boilerplate we extracted.

## Specification<T, TResult> — base class

```csharp
// src/shared/Plexor.Shared.Persistence/ISpecification.cs
public interface ISpecification<T>
{
    IQueryable<T> Apply(IQueryable<T> query);
}

public interface ISpecification<T, TResult>
{
    IQueryable<TResult> Apply(IQueryable<T> query);
}

// Base implementation with fluent composition:
public abstract class Specification<T, TResult> : ISpecification<T, TResult>
    where T : class
{
    private protected List<Expression<Func<T, bool>>> WhereClauses { get; } = [];
    private protected Func<IQueryable<T>, IOrderedQueryable<T>>? OrderByClause { get; set; }
    private protected Expression<Func<T, TResult>>? Projection { get; set; }
    private protected bool AsNoTrackingFlag { get; set; }
    private protected bool IncludeIncludes { get; set; }

    public abstract IQueryable<TResult> Apply(IQueryable<T> query);

    public Specification<T, TResult> WithWhere(Expression<Func<T, bool>> predicate);
    public Specification<T, TResult> WithOrderBy<TKey>(Expression<Func<T, TKey>> keySelector);
    public Specification<T, TResult> WithOrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector);
    public Specification<T, TResult> AsNoTracking();
    public Specification<T, TResult> Paginate(int page, int pageSize);

    // Projection only — no filter/order.
    public static Specification<T, T> Identity() => new IdentitySpec<T>();
    public static Specification<T, TResult> Default(Expression<Func<T, TResult>> projection)
        => new ProjectionSpec<T, TResult>(projection);
}
```

### Spec by example

```csharp
// Cluster read spec — derives from ClustersByOrgSpec and adds Active filter
public sealed class ClustersByOrgSpec : Specification<Cluster, ClusterSummary>
{
    public ClustersByOrgSpec(Guid orgId)
    {
        WithWhere(c => c.OrgId == orgId);
        WithOrderByDescending(c => c.CreatedAt);
        Projection = c => new ClusterSummary(...);
        AsNoTracking();
    }
}

// Reuse: combine two specs into a new composed spec
public sealed class ActiveClustersInRegionSpec : Specification<Cluster, ClusterSummary>
{
    public ActiveClustersInRegionSpec(Guid orgId, string region)
        : this()
    {
        WithWhere(c => c.Region == region && c.Status == ClusterStatus.Ready);
    }
}
```

### When NOT to use Specification
- Single-entity fetch (`repo.GetByIdAsync(id)`)
- Truly trivial single-line query
- 1-off query that won't be reused (just call `Repository<T>.FirstOrDefaultAsync(where predicate)`)

## Plexor.Shared.Filtering — DSL parser, used by PageAsync

Non-DSL spec endpoints accept `FilterQuery` from the URL. Use
`Plexor.Shared.Filtering`:

```csharp
[HttpGet("")]
public async Task<ActionResult<PageResult<ClusterSummary>>> ListAsync(
    [FromQuery] FilterQuery query,
    [FromServices] IRepository<Cluster> repo,
    [FromServices] ClusterFieldSet fields,
    CancellationToken ct = default)
{
    var spec = new ClusterSummariesSpec();
    return Ok(await repo.PageAsync(spec, query, fields, ct));
}
```

- DSL: `name~John;status==Active` (AND), `(a|b)` (OR), `now(-7d)`, `[]=Active,Trial` (IN)
- Sort: `name,asc;createdAt,desc` (multi-criteria, OrderBy + ThenBy)
- `QueryableFilterExtensions.ApplyFilter/ApplySort` — extensions on `IQueryable<T>`
- `FilterableFieldSet<TEntity>` — per-entity field registry via reflection

`PageAsync` wires these together: spec applies first (composable baseline
filter), then DSL applies on top (URL-supplied), then sort, then count + paginate.

## Schema-per-module — DbContext per module, snake_case columns

```csharp
public sealed class ClusterDbContext(DbContextOptions<ClusterDbContext> options) : PlexorDbContext(options)
{
    public DbSet<Cluster> Clusters => Set<Cluster>();
    public DbSet<Node> Nodes => Set<Node>();
    public DbSet<JoinToken> JoinTokens => Set<JoinToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Clusters)
            .ApplyConfiguration(new ClusterConfiguration())
            .ApplyConfiguration(new NodeConfiguration())
            .ApplyConfiguration(new JoinTokenConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
```

Per `coding/ef-core.md`: snake_case columns via `HasColumnName("snake_case")`,
explicit `HasMaxLength` on strings, indexes via `HasDatabaseName("snake_case")`.

## Self-audit

```bash
# Repository<T> usage — handlers should inject IRepository<T> (the base class) for reads
rg -n "Repository<\w+>" src/ --type cs

# No generic interface IRepository<T>
rg -n "interface IRepository<\w+>" src/ --type cs
# Должно быть пусто (базовый класс абстрактный, не интерфейс).

# Specification usage
rg -n "Specification<" src/ --type cs

# PageAsync callers
rg -n "\.PageAsync\(" src/ --type cs

# DbContext usage in HANDLERS — write/aggregate only
rg -n "ClusterDbContext\b" src/ --type cs
# Should appear ONLY in:
#   - Persistence/ClusterDbContext.cs (definition)
#   - Persistence/ClusterDbContextFactory.cs (design-time factory)
#   - WRITE handlers in Infrastructure/Clusters/ (add/update/delete)
#   - Persistence/*Configuration.cs (entity configs)
# Read handlers should depend on IRepository<Cluster>, not on ClusterDbContext.
```

## Anti-patterns

- ❌ `interface IRepository<T>` — бойлерплейт-обёртка. See "Where Repository IS still anti-pattern" above.
- ❌ Custom `Repository<T>` subclass that re-wraps each method — extend the base.
- ❌ Handler depending on both `IRepository<T>` AND `DbContext` for the *same* entity (split seams — choose one per handler based on intent).
- ❌ `Repository<T>.ListAsync()` with no spec parameter (re-introduces the "fetch all" anti-pattern).
- ❌ `ToList()` on a paged query — materializes the entire table; always use Skip/Take + Count.
- ❌ Specification without projection (`Spec<Cluster, Cluster>`) used where a projection helps — load full entity when 3 columns would do. Push `.Select(...)` into the spec.
- ❌ Skip migrations in dev — `Add-Migration` обязателен, schema-per-module — никаких "manual" SQL.
- ❌ `Repository<T>.AddAsync` from a read endpoint / `Delete` from a write endpoint that doesn't need them — only the methods you actually call.
- ❌ Handler-level `AsNoTracking()` calls — AsNoTracking belongs on the spec via `AsNoTracking()`. The repo shouldn't have a no-tracking version of each query method.
