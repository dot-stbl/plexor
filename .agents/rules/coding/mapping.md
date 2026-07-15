---
description: plexor entity-to-DTO mapping pattern. Use Mapperly source generators + I{Entity}Mapper interface abstraction. Partial sealed class DTOs (not records) for init-only property compatibility.
globs: ["**/Mappers/**/*.cs", "**/Application/**/*Dto.cs", "**/Application/**/*Summary.cs", "**/Application/**/*Detail.cs"]
always: false
---

# Mapping (entity → DTO)

Plexor maps between domain entities and public DTOs (returned by
controllers / serialized to JSON) through **Mapperly source-generated
mappers**, fronted by a per-module **interface** so handlers depend on
the contract, not the concrete class.

## Stack

| Piece | Library / Convention |
|-------|---------------------|
| Source generator | **Riok.Mapperly** (10.x in `Directory.Packages.props`) |
| Interface per module | `I{Module}Mapper` in `*.Infrastructure/Mappers/` (singular — one mapper per module, NOT plural `IMappers`) |
| Concrete implementation | `partial sealed class {Module}Mapper : I{Module}Mapper` with `[Mapper]` attribute (singular — `SigilMapper`, not `SigilMappers`) |
| DTO shape | `sealed partial class` with init-only properties (NOT `record` — see below) |
| Test pattern | Construct a real `{Module}Mapper()` instance — generated bodies are deterministic and unit-testable directly |

## Naming — **singular, not plural**

```csharp
// ✅ Default — one mapper per module, singular
public interface ISigilMapper { ... }                       // interface
public sealed partial class SigilMapper : ISigilMapper { }   // concrete

// ❌ Wrong — the mapper is one object with multiple methods,
//    not a collection of multiple objects. Plural `mappers` reads
//    as "a group of mapper objects" which is wrong.
public interface ISigilMappers { ... }
public sealed partial class SigilMappers : ISigilMappers { }
```

**Rule.** The mapper is one instance with N mapping methods (one
per public DTO projection). Method names describe the destination
type (singular): `ToUserSummary`, `ToApiKeySummary`, `ToNodeSummary`,
not `ToUserSummaries` or `MapUserToSummary`. The mapper itself is
named `{Module}Mapper` (singular) and its interface is
`I{Module}Mapper` (singular).

This matches the handler parameter convention — handlers inject
`IClusterMapper mapper` (singular), not `IClusterMappers mappers`.

If a module ends up with more mappers than fit in one class (rare;
usually 3–8 methods is fine), split into
`{Aggregate}Mapper : I{Aggregate}Mapper` per aggregate root, not
`{Aggregate1}Mappers` / `{Aggregate2}Mappers`.

## DTOs are `sealed partial class` with init properties, NOT records

```csharp
// ✅ Default — Mapperly + EF Core friendly
public sealed partial class ClusterSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    // ...
}

// ❌ Wrong — Mapperly cannot target positional records
public sealed record ClusterSummary(Guid Id, string Name, ...);
```

`sealed record` (positional) breaks the Mapperly source generator:
- Mapperly tries to emit `new T(...)` with positional args from the
  generated mapping body.
- The generated `.g.cs` calls the record's primary constructor
  positionally, but a `record` constructor is treated as "no
  accessible constructor with mappable arguments" by Mapperly 4.x.
- EF Core LINQ projections (`Select(... new X { Prop = ... })`) work
  fine with the `partial class` + object-initializer pattern.
- Value-equality on DTOs is not needed (DTOs are JSON-serialized,
  not compared in-memory).

## Mapper interface + implementation

```csharp
// src/modules/<X>/<X>.Infrastructure/Mappers/I<Module>Mapper.cs
public interface I<Module>Mapper
{
    <Summary> ToSummary(<Entity> source);

    <Detail> ToDetail(<Entity> source, IReadOnlyList<NodeSummary> nodes);

    NodeSummary ToNodeSummary(Node source);
}

// src/modules/<X>/<X>.Infrastructure/Mappers/<Module>Mapper.cs
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class <Module>Mapper : I<Module>Mapper
{
    // Target must be mapped (a missing mapping breaks the build).
    [MapperIgnoreTarget(nameof(<Summary>.NodeCounts))]
    public partial <Summary> ToSummary(<Entity> source);

    // MapProperty not needed: the additional parameter name
    // ("nodes") matches the target property "Nodes" by name
    // (case-insensitive).
    public partial <Detail> ToDetail(<Entity> source, IReadOnlyList<NodeSummary> nodes);

    public partial NodeSummary ToNodeSummary(Node source);
}
```

### RequiredMappingStrategy = Target (default)

Every target DTO property must be mapped. A missing mapping breaks the
build with `RMG005`, catching a silent runtime bug at compile time.

- Computed properties (aggregations, lookups) → `[MapperIgnoreTarget]`
  + caller post-maps (e.g. `with` expression, setter, or recompute
  via repository call).
- Properties sourced from additional method parameters → handled
  automatically by parameter name (case-insensitive match).

### DI registration

```csharp
// Singleton: generated bodies are stateless, allocation-free.
services.AddSingleton<I<Domain>Mapper, <Domain>Mappers>();
```

Handlers depend on the interface:

```csharp
public sealed class GetClusterQueryHandler(
    Repository<Cluster> clusterRepo,
    Repository<Node> nodeRepo,
    IClusterMapper mapper) : ICommandHandler<GetClusterQuery, ClusterDetail>
{
    public async Task<ClusterDetail> HandleAsync(
        GetClusterQuery query,
        CancellationToken cancellationToken = default)
    {
        if (await clusterRepo.FirstOrDefaultAsync(
                new ClusterByIdSpec(query.ClusterId), cancellationToken)
                is not { } cluster)
        {
            throw new ClustersException(ClustersExceptions.ClusterNotFound, ...);
        }
        var nodes = await nodeRepo.ListAsync(
            new NodesByClusterSpec(query.ClusterId), cancellationToken);
        return mapper.ToDetail(cluster, nodes);  // 1 line, no 13-field constructor
    }
}
```

## Parameter name convention

Use `source` for the source parameter — matches Mapperly best-practice
guidance and makes the mapping direction obvious at the call site.

```csharp
public partial ClusterSummary ToSummary(Cluster source);
```

## Test pattern

```csharp
// Unit tests construct the real generated mapper — bodies are
// deterministic and don't need mocking.
var mapper = new ClusterMappers();
var handler = new GetClusterQueryHandler(
    new ClusterRepository(db),
    new NodeRepository(db),
    mapper);
```

If you need to verify a specific mapping branch (e.g. error
handling), NSubstitute a custom `IClusterMapper` with explicit
`.Returns(...)`. Default `Substitute.For<IClusterMapper>()` returns
`null` for unconfigured methods, which surfaces as `NullReferenceException`
deep in the handler — configure every method the test exercises.

## EF Core LINQ projection — also fine with `partial class`

`partial class` DTOs compose with EF Core's `Select(... new X { Prop = ... })`
projection just as well as positional records do — the source generator
emits object-initializer syntax. For complex projection in a
Repository's PageAsync, prefer the Repository + FilterableFieldSet +
Mapperly pipeline over inline `new T { ... }` blocks (see
`architecture/persistence.md`).

## Anti-patterns

- ❌ `sealed record` DTOs — breaks Mapperly. See "DTOs are partial
  class" above.
- ❌ `Substitute.For<IMapper>()` without `.Returns(...)` — NRE in
  handler. Always configure every method the test calls.
- ❌ Hand-built 13-field constructor calls in handlers — exactly what
  Mapperly is for.
- ❌ `[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]` —
  silently misses unmapped properties; defeats the whole point of
  strict mappings.
- ❌ Mapper methods that take a positional DTO as source (e.g.
  `ToDetail(ClusterDetail source)`) — mappers map entity → DTO,
  never DTO → DTO (that's a different concern, e.g. version
  conversion, which doesn't apply here).

## Self-audit

```bash
# Mappers live in *.Infrastructure/Mappers/ — one directory per module
ls src/modules/Plexor.Modules.<X>/Plexor.Modules.<X>.Infrastructure/Mappers/

# All DTOs are partial class, not record
rg -in "public sealed record.*(Summary|Detail|Dto)\(" src/ --type cs
# Должно быть пусто (records with these suffixes — only commands/results,
# never DTOs).

# Every mapper class has [Mapper(RequiredMappingStrategy = Target)]
rg -n "RequiredMappingStrategy = RequiredMappingStrategy.Target" src/ --type cs
# Each Mappers/<X>Mappers.cs has it.

# No inline 10+ field constructor calls in handlers
rg -n "new ClusterSummary\(" src/ --type cs
# Должно быть пусто — only ToSummary(mapper) / ToDetail(mapper) calls.
```

## Related rules

- `architecture/persistence.md` — Repository<T> + Specification<T, TResult>
  + PageAsync filter DSL — the read pipeline that hands off to
  mappers at the end of the chain.
- `coding/anti-patterns.md` §2 — DTO records were the old convention;
  this rule supersedes that for DTOs specifically.
- `coding/constructors-and-fields.md` — primary ctor for DI classes
  (handlers); mappers are stateless, no ctor needed.
