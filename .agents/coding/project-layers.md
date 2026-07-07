---
description: project layers overview — application, feature, database, client, models, shared, generation, frontend, tests
globs: ["**/*.csproj", "**/console.x.slnx"]
always: true
---

# Project layers

Этот файл — обзор слоёв solution. Naming/decision tree/setup — в
`project-naming-and-setup.md`. Layer dependencies + testing structure —
в `project-deps-and-tests.md`.

## 1. Layers overview

Архитектура — модульная по capability, не DDD. Сверху вниз:

```
application/    ← entry points: API, workers, aggregator, collector
bots/           ← внешние интерфейсы (Telegram bot, etc.)
feature/
  patterns/     ← торговые паттерны с доменной логикой
  specified/    ← узкие реализации общих концептов
  <other>       ← level-0 capability blocks
database/       ← persistence: DbContext, миграции
client/         ← C# HTTP-клиенты к нашему API
models/         ← entities + API DTO
shared/         ← cross-cutting (DI, logging, extensions)
generation/     ← Roslyn source generators, analyzers
frontend/       ← веб-фронт (изолирован от .NET)
tests/          ← тесты, разделённые на unit/ + integration/
```

В одном solution — **один** префикс.

---

## 2. Layer responsibilities

### `shared/`, `feature/`, `feature/patterns/`, `feature/specified/`

- **`shared/`** — cross-cutting инфраструктура (DI bootstrap, logging,
  extensions, MSBuild props). Без этого слой не запустится, но без
  бизнес-смысла.
- **`feature/`** — независимые блоки, **ровно один** capability. Выносить
  в проект когда: переиспользуется в 2+ application-проектах или > 10
  файлов и логически изолирован.
- **`feature/patterns/`** — торговые паттерны с доменной логикой (один
  проект = один паттерн).
- **`feature/specified/`** — узкие реализации общих концептов из
  `Pattern.Core` или `shared/`. Не паттерны и не capability.

| Признак | Куда |
|---------|------|
| Новый capability | `feature/<Name>/` |
| Специализация существующего концепта | `feature/specified/<Name>/` |
| Торговый паттерн | `feature/patterns/<Name>/` |

### `database/` — persistence

**Принцип:** один `DbContext` = один проект.

```
database/
├── Contoso.Crm.Database.Core/              # общая инфраструктура (если есть)
├── Contoso.Crm.Database.Customers/         # один DbContext = один проект
│   ├── InventoryDbContext.cs
│   ├── Migrations/                         # миграции только этого контекста
│   └── Extensions/ServiceCollectionExtensions.cs
└── ...
```

`Database.Core` нужен при **любом** из условий: Specification pattern,
generic repository, общие EF extensions, base entity типы.

**Не должно быть:** HostedService/BackgroundService, бизнес-логика.

### `client/` и `models/`

- **`client/`** — C# HTTP-клиенты к нашему API. Один проект = один API.
  Типизированный интерфейс (`IPublicApiClient`), DI extension
  (`AddPublicApiClient`), DTO из `models/...Api.Contracts`.
- **`models/`** — `Entity.Core` (entities, EF-атрибуты) и `Api.Contracts`
  (DTO для JSON, без EF-атрибутов).

```
models/
├── Acme.Shop.Entity.Core/        # доменные entities + общие интерфейсы
└── Acme.Shop.Api.Contracts/      # HTTP API DTO
```

### `application/` — entry points

```
application/
├── api/                  # публичные HTTP endpoints
│   └── Acme.Shop.Api.Public/
└── internal/             # workers/aggregators/collectors
    ├── Northwind.Logistics.Aggregator/
    └── Northwind.Logistics.Collector/
```

Только `Program.cs` + `appsettings.json` + тонкий composition root.
**Не должно быть:** бизнес-логики, repositories, services.

### `bots/`, `generation/`, `frontend/`, `tests/`

- **`bots/`** — те же entry points, но event-driven / polling вместо HTTP.
- **`generation/`** — Roslyn source generators, analyzers, code-fixers.
  Изолированы: `netstandard2.0`, не зависят от runtime-проектов.
- **`frontend/`** — изолирован от .NET. Свой `package.json`, lock-файл
  рядом (не в корне).
- **`tests/`** — см. `project-deps-and-tests.md` §3.

---

## Связанные правила

- `project-naming-and-setup.md` — naming, decision tree, новый проект
- `project-deps-and-tests.md` — layer dependencies, testing structure