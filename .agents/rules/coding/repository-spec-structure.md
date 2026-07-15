---
description: where Repository<T> subclasses, Specifications, and per-module DbContext classes live in the project tree. Enforces a flat per-entity folder layout per .agents/rules/architecture/persistence.md.
globs: ["**/Infrastructure/Persistence/**/*.cs", "**/Domain/**/*.cs"]
always: false
---

# Repository + Specification folder layout (per module)

Правило к `.agents/rules/architecture/persistence.md`. Фиксирует расположение
`Repository<T>`, `Specification<T, TResult>` и связанного с ними в проекте.

## Why per-module and not centralised

`Plexor.Shared.Persistence` содержит **базовый** `Repository<T>`,
`Specification<T, TResult>` и интерфейсы `ISpecification<...>`. Они generic
для всех модулей и не знают про конкретные DbContext / entity.

Специализация — per-module subclass + конкретные specifications — живёт в
**модуле**:

```
src/modules/Plexor.Modules.<Module>/Plexor.Modules.<Module>.Infrastructure/
├── Persistence/
│   ├── <Module>DbContext.cs              # DbContext (schema, OnModelCreating)
│   ├── <Module>DbContextFactory.cs       # IDesignTimeDbContextFactory (ef tools)
│   ├── <Entity>Repository.cs            # one Repository<T> subclass per DbSet
│   ├── <Entity>Repository.cs            # (continue per entity)
│   ├── Specifications/
│   │   ├── <Entity>Specs.cs             # one file per entity with its ISpecification<,> derivations
│   │   ├── <OtherEntity>Specs.cs
│   │   └── ...
│   └── Migrations/
│       ├── <ContextName>/
│       │   ├── <timestamp>_<Name>.cs
│       │   └── <ContextName>DbContextModelSnapshot.cs
│       └── ...
├── Installers/
│   └── <Module>InfrastructureInstaller.cs
└── ...
```

## Why one `Specifications/<Entity>Specs.cs` per entity

Specs живут группами по entity, не "один файл на одну спецификацию".
Аргументы:

- `ClustersByOrgSpec` + `ClusterByIdSpec` оба фильтруют `Cluster`.
  Если бы каждый в своём файле — при добавлении ещё одного spec для
  `Cluster` пришлось бы создавать третий файл. Сгруппировано — добавил
  в `ClusterSpecs.cs`, и grep по имени сущности сразу находит всё.
- Co-located composition: `ClustersByOrgSpec.WithRegion(...)` живёт
  рядом с базовой `ClustersByOrgSpec` — это та же группа спецификаций.
- Число файлов = числу сущностей, не числу спецификаций. У `Cluster`
  может быть 5 specs; у `JoinToken` — 1. Два файла.

## Anti-patterns

- ❌ `Persistence/Repositories/<Entity>Repository.cs` — лишний уровень
  вложенности. У Plexor модулей обычно 3–6 DbSet'ов; flat layout с
  `ClusterRepository.cs`, `NodeRepository.cs` рядом с DbContext
  достаточен.
- ❌ `Persistence/<Entity>Specifications/Spec.cs` — то же: спецификации
  **группируются по entity**, не по отдельным файлам на spec.
- ❌ `Plexor.Shared.Persistence/Modules/<X>` — каждое module в
  общей shared-библиотеке. Shared — только базовые примитивы
  (Repository<T>, Specification<T, TResult>, ISpecification<,>),
  SpecificationFactory, FilterableFieldSet, extensions). Модули
  наследуют, не наполняют shared.
- ❌ `Specs/Query/Commands/Separation` — папки по типу использования
  (queries vs commands). Спецификации — это criteria objects, не
  command/query handler-разделение. Группируются по entity.

## Self-audit

```bash
# Per-module: one Repository<T> subclass per DbSet — verify
ls src/modules/Plexor.Modules.<X>/Plexor.Modules.<X>.Infrastructure/Persistence/
# Expected: <Module>DbContext.cs + one <Entity>Repository.cs per DbSet

# Per-module: one Specifications/<Entity>Specs.cs per entity — verify
ls src/modules/Plexor.Modules.<X>/Plexor.Modules.<X>.Infrastructure/Persistence/Specifications/
# Expected: one <Entity>Specs.cs file per entity that has reads

# No specs leak into Shared — only the base class + factory
ls src/shared/Plexor.Shared.Persistence/
# Expected: Repository.cs, Specification.cs, SpecificationFactory.cs,
#           ISpecification.cs (+ Extensions/, Filters/, etc.)
# NOT: anything per-module like ClusterRepository.cs

# Repositories registered as Scoped (matches DbContext lifetime)
rg -n "services\.AddScoped<\s*Repository<\w+>" src/
```
