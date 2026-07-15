---
phase: 5
plan: plan-clusters
title: "Plexor.Modules.Clusters — self-hosted control plane fleet (backend Phase 1)"
status: complete
duration: "~6h"
started: 2026-07-14T20:00:00Z
completed: 2026-07-15T23:00:00Z
tasks_completed: 11
files_modified: 35
tags: [clusters, schema-forge, repository-specification, mapperly, ef-migrations]
key-files:
  created:
    # Domain
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Domain/Cluster.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Domain/Node.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Domain/JoinToken.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Domain/NodeRole.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Domain/Errors/ClustersException.cs
    # Application
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Application/Clusters/ClusterCommands.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Application/Clusters/NodeCommands.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Application/Abstractions/ICommandHandler.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Application/Authorization/ClusterPermissions.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Application/Installers/ClustersApplicationInstaller.cs
    # Infrastructure — handlers
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Clusters/ClusterCommandHandlers.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Clusters/ClusterReadHandlers.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Clusters/NodeCommandHandlers.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Clusters/TokenHasher.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Errors/ClustersExceptionHandler.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Installers/ClustersInfrastructureInstaller.cs
    # Infrastructure — persistence
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Persistence/ClusterDbContext.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Persistence/ClusterDbContextFactory.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Persistence/ClusterRepository.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Persistence/NodeRepository.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Persistence/JoinTokenRepository.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Persistence/Specifications/ClusterSpecs.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Persistence/Specifications/NodeSpecs.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Persistence/Specifications/JoinTokenSpecs.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Migrations/Clusters/20260715124344_Clusters_InitialSchema.cs
    # Infrastructure — mappers (Mapperly)
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Mappers/IClusterMapper.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Infrastructure/Mappers/ClusterMapper.cs
    # API
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Api/Controllers/ClustersController.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Api/Controllers/NodeAgentController.cs
    - src/modules/Plexor.Modules.Clusters/Plexor.Modules.Clusters.Api/Models/ClusterRequests.cs
    # Tests
    - tests/unit/Plexor.Modules.Clusters.Unit/Clusters/ClusterQueryHandlersShould.cs
    - tests/unit/Plexor.Modules.Clusters.Unit/Clusters/CreateClusterCommandHandlerShould.cs
    - tests/unit/Plexor.Modules.Clusters.Unit/Clusters/DeleteClusterCommandHandlerShould.cs
    - tests/unit/Plexor.Modules.Clusters.Unit/Clusters/NodeJoinCommandHandlerShould.cs
    - tests/unit/Plexor.Modules.Clusters.Unit/TestDb.cs
  modified:
    - plexor.slnx
    - src/host/Plexor.Host/Plexor.Host.csproj
    - src/host/Plexor.Host/Program.cs
    - src/host/Plexor.Migrator/Plexor.Migrator.csproj
    - Directory.Packages.props
    - .editorconfig
    - .agents/rules/architecture/persistence.md
    - .agents/rules/coding/mapping.md
    - .agents/rules/coding/repository-spec-structure.md
    - .agents/rules/coding/code-shape.md
    - .planning/BACKEND-ISSUES.md
---

# Plan: Clusters backend — outcome

## What shipped

`Plexor.Modules.Clusters` — first end-to-end Plexor module — is wired
end-to-end against a real PostgreSQL instance. Plexor's persistence
foundation (`Repository<T>` + `Specification<T, TResult>` +
`Plexor.Shared.Filtering.PageAsync<TResult>`) lands in this same
plan; subsequent modules will reuse it without a separate
foundation phase.

### Modules

| Layer | Project | Role |
|---|---|---|
| Domain | `Plexor.Modules.Clusters.Domain` | `Cluster` + `Node` + `JoinToken` aggregates + `ClustersException` + `ClusterStatus` / `NodeStatus` / `NodeRole` / `TokenStatus` / `NodeSpec` / `NodeCounts`. All `sealed class` with init-only props; implement `IFilterableEntity`, `ICreatedAt`, `IUpdatedAt`. |
| Application | `Plexor.Modules.Clusters.Application` | `ClusterCommands` (`Create`/`Update`/`Delete`) + `NodeCommands` (`Join`/`Heartbeat`/`Update`/`Drain`) + permissions (`compute.clusters.{create,read,update,delete}` + `compute.nodes.read`) + `ICommandHandler<TCommand, TResult>` contract. |
| Infrastructure | `Plexor.Modules.Clusters.Infrastructure` | `ClusterDbContext` (schema `forge`, FK constraints `OnDelete(Restrict)`, snake_case migrations) + EF Core migration `20260715124344_Clusters_InitialSchema` + per-entity `Repository<T>` subclasses + `Specification<T, TResult>` classes + write/read handlers + `TokenHasher` (PBKDF2) + `IClusterMapper` (Mapperly-generated) + `ClustersExceptionHandler` + `ClustersInfrastructureInstaller`. |
| API | `Plexor.Modules.Clusters.Api` | `ClustersController` (7 endpoints: list/create/get/update/delete + nodes-in-cluster + tokens-by-cluster), `NodeAgentController` (join + heartbeat), `Models/ClusterRequests.cs` request DTOs (all `sealed partial class` with init-only props for Mapperly targeting). |

### Persistence foundation (shared)

`src/shared/Plexor.Shared.Persistence/` gained a clean
read/write seam:

- **`Repository<T>`** — abstract base. Per-module subclass wires
  `protected override IQueryable<T> Query => db.Set;`. Provides
  `ListAsync(spec)` ×2 (entity + projection),
  `CountAsync(spec)`, `GetByIdAsync<TId>(id)`,
  `FirstOrDefaultAsync(spec)` ×2. NO `IRepository<T>` interface
  (anti-pattern — leaks EF specifics).
- **`Specification<T, TResult>`** — fluent: `WithWhere`, `WithOrderBy`,
  `WithOrderByDescending`, `AsNoTracking`, `Paginate`. Immutable
  decorator pattern via `MemberwiseClone()`. `SpecificationFactory`
  exposes non-generic `Identity<T>()` + `Default<T, TResult>(projection)`
  for CA1000-friendly usage.
- **`PageAsync<TResult>(ISpecification<T>, Expression<Func<T, TResult>>, FilterQuery, FilterableFieldSet<T>, CT)`**
  — one round-trip per call: spec.Apply → ApplyFilter (URL DSL)
  → ApplySort (URL DSL) → Count + Skip/Take + Select(projection).
  Single seam for paginated lists across all modules.
- **`AddPlexorModuleDbContexts(connectionString)`** — reflects
  `PlexorDbContext` subclasses; per-module
  `services.AddScoped<Repository<T>, TRepository>()`. Filterable
  field-sets are singleton via `FilterableFieldRegistry.For<T>()`.

### Mapperly integration

`IClusterMapper` (singular interface) + `ClusterMapper` (`partial
sealed class`, `[Mapper(RequiredMappingStrategy = Target)]`) — 5
projection methods project `Cluster`/`Node` aggregates into
`ClusterSummary` / `ClusterDetail` / `NodeSummary` DTOs. Handlers
use `mapper.ToSummary(cluster)`, `mapper.ToDetail(cluster, nodes)`,
`mapper.ToNodeSummary(n)` — `13`-field constructors collapse to
one line each.

DTOs were converted from `sealed record` (positional ctor) to
`sealed partial class` + init-only props after Mapperly 4.x's
RMG013 ("no accessible constructor with mappable arguments")
rejected positional records as mapping targets.

`ISigilMapper` + `SigilMapper` propagated the same pattern to
`UserSummary` / `ApiKeySummary` / `SshKeySummary` / `RoleSummary`
/ `RoleBindingSummary`. All Sigil DTOs and Clusters DTOs now use
the partial-class form consistently.

### End-to-end verification

`MapprlyApiSmokeTests` (in `Plexor.Host.UnitTests`, real
Postgres on `Host=localhost;Port=47100`) seeds a row in
`sigil.users` through EF Core, fetches it via `AsNoTracking()
.FirstAsync()` and runs `mapper.ToUserSummary(user)` against the
source-generated body. Test passes; Mapperly projection works
through the live Postgres pipeline.

Unit tests for Clusters:
- `ClusterQueryHandlersShould` — list/get pagination + ordering
- `CreateClusterCommandHandlerShould` — happy-path create + event raised
- `DeleteClusterCommandHandlerShould` — guards on missing cluster
- `NodeJoinCommandHandlerShould` — join with token + first node wires cluster to Active

All 11 + 8 Sigil + 7 Host unit tests green; build 0 warnings /
0 errors.

### Schema

Postgres on compose-managed podman (port 47100). New `forge` schema
created via Migrator. Tables (all `OnDelete(Restrict)` because
`Cluster.Nodes` / `Cluster.Tokens` are `Ignore()`'d for InMemory-
DbContext collection-nav compatibility):

```
forge.clusters      (id, org_id, name, region, status, install_providers[text[]],
                     join_endpoint, created_at, updated_at)
forge.nodes         (id, cluster_id, hostname, role, status, agent_version,
                     cpu_cores, ram_mb, disk_gb, last_heartbeat_at,
                     drained_at, created_at, updated_at)
forge.join_tokens   (id, cluster_id, secret_hash, expires_at,
                     revoked_at, created_at, issued_at, status)
```

FK constraints explicit because the EF navigations are `Ignore()`'d
— without `HasOne<Cluster>().WithMany().HasForeignKey(...).OnDelete(Restrict)`
EF Core cannot infer the cascade behaviour.

## Decisions (captured)

- **`sealed partial class` over `sealed record` for DTOs** — Mapperly
  4.x requires a target type whose source can map into a single ctor.
  Positional records don't satisfy this requirement; partial classes
  with init-only props do. Convention enforced via
  `.agents/rules/coding/mapping.md`.
- **Mapperly name is singular** — one instance per module with N
  methods. `IMappers` would read as "multiple mapper objects" which
  is wrong. Convention encoded in `mapping.md` and validated by the
  per-module grep.
- **Tests use real mapper instance** — `new ClusterMapper()`,
  not `Substitute.For<IClusterMapper>()`. NSubstitute's default null
  return triggers NRE deep in handler calls; trivial generated
  bodies test cleanly via the concrete instance.
- **`ToArrayAsync` over `ToListAsync` everywhere** — EF Core
  materialises directly into `T[]`. `List<T>` is reserved for code
  that mutates the collection. Allocates less, passes `IReadOnlyList<T>`
  via implicit implementation.
- **`Argument*.ThrowIf*` family banned project-wide.** nullable
  annotations already enforce non-null at call sites; CA1062 disabled
  in `.editorconfig`. Forward-only cleanup deleted 76 pre-existing
  violations. Convention is in `.agents/rules/coding/code-shape.md §11`.
- **`MA0006` disabled in `.editorconfig`** — `string.Equals(a, b, comparison)`
  doesn't translate to Postgres SQL via Npgsql. Use `==` for strings in
  handlers / specs / queries. Owner-style: rule documented with rationale.
- **One IL per module** (singular) — `IClusterMapper`/`ClusterMapper`,
  `ISigilMapper`/`SigilMapper`. Plural reads as "a group of mapper
  objects" which is wrong; convention documented in `mapping.md`.

## Not done (deferred)

- **Sigil captive-dependency DI cluster** — 8 services form a
  captive-dep cycle (documented in `.planning/BACKEND-ISSUES.md`).
  Production is unaffected; WebApplicationFactory + Development both
  validate. Resolves through `IServiceScopeFactory` injection in 4
  Sigil files before the next module's API tests land.
- **plan-clusters frontend end-to-end** — `use-clusters` /
  `use-join` exist but have not been wired against the real
  `forge` schema. Defer to the `web/i18n` continuation; backend
  smoke test confirms the mapper pipeline independent of browser.
- **Hub integration (Nodes ↔ Workloads)** — `JoinToken.IssuedAt`
  + node count trigger still call `Cluster.Ready++;` directly on
  the in-memory `NodeCounts` instance; full distributed durability
  is a Phase 6+ concern (NATS outbox relay).

## What unblocks

- `plan-runtime-providers` (Workloads registry) — builds directly
  on `Cluster` as the deployment surface.
- `plan-k8s` — first app provider; needs `forge.clusters` aggregate.
- Mapperly pattern is reusable as-is for every future module's DTO
  shape (`Plexor.Modules.Realm` will reuse on its first DTO-bearing
  endpoint).
