---
description: layer dependencies (только вниз), testing structure (unit/ + integration/), anti-patterns организации проекта
globs: ["**/*.csproj", "**/console.x.slnx"]
always: true
---

# Layer dependencies & testing structure

Этот файл — правила ссылок между слоями, структура тестов, anti-patterns
организации. Layers overview — в `project-layers.md`. Naming/setup — в
`project-naming-and-setup.md`.

## 1. Layer dependencies — правило: ссылки только вниз

```
application/  ─┐
bots/         ─┤
               ├──→  feature/   ─→  models/   ─→  shared/
generation/   ─┘                ─→  database/ ─┘

feature/patterns/    ─→  feature/  (можно)
feature/specified/   ─→  feature/  (можно)
feature/             ─→  models/, shared/  (можно)

feature/patterns/    ─×  application/  (нельзя)
shared/              ─×  feature/      (нельзя)
models/              ─×  database/     (нельзя)
```

### Конкретные разрешённые ссылки

| Слой | Может ссылаться на |
|------|-------------------|
| `application/` | `feature/`, `database/`, `client/`, `models/`, `shared/`, `bots/` |
| `bots/` | `feature/`, `client/`, `models/`, `shared/` |
| `feature/patterns/`, `feature/specified/` | `feature/`, `models/`, `shared/` |
| `feature/<other>` | `models/`, `shared/` (**НЕ** другие feature) |
| `database/<...>.Database.<Name>` | `Database.Core`, `models/`, `shared/` |
| `database/Database.Core` | `models/`, `shared/` |
| `client/` | `models/`, `shared/` |
| `models/` | `shared/` (минимально, лучше — ничего) |
| `shared/`, `generation/` | ничего (только nuget) |
| `tests/` | любое из `src/` |

### Между проектами одного слоя

- `feature/<other>` **НЕ** ссылаются друг на друга. Общее → `shared/`.
- `feature/patterns/*` → `Pattern.Core` (базовые абстракции); между
  конкретными паттернами — нет.
- `database/<...>.Database.<Name>` **НЕ** ссылаются друг на друга.
  Cross-db связи — на application-уровне.

Проверяется автоматически в `tests/architecture/` через NetArchTest
(см. `class-layout-and-tooling.md` §3).

---

## 2. Anti-patterns

### Технический долг в имени папки

```
❌ database/.../Repositories/        # "(бывшие, вынесены)"
❌ feature/.../Services_Old/
❌ shared/.../Deprecated/
```

Либо удалить сразу, либо issue с дедлайном. Не хранить "на всякий случай".

### Циклы зависимостей через DI

```csharp
// ❌ feature/A регистрирует реализацию из feature/B → cycle
services.AddSingleton<ISomething, SomethingFromFeatureB>();
```

Решение: общая абстракция в `shared/` или `models/`, реализации
регистрируются на application-уровне.

### Бизнес-логика в `application/`

Application — **только** composition + bootstrap. Расчёты, торговые решения
— в `feature/patterns/`.

### Persistence в feature

`feature/` работает через **интерфейсы** репозиториев / query services,
реализации — в `database/`. Позволяет тестировать паттерны без БД.

### Утечка EF-атрибутов в DTO

API контракты (`Acme.Shop.Api.Contracts`) не знают про EF Core. Никаких
`[Table]`, `[Column]`, `[ForeignKey]`.

### Папки с именами-помойками

`Helpers/`, `Utils/`, `Common/`, `Misc/`, `Tools/`, `Stuff/`.

### Один большой проект вместо нескольких

```
❌ Fabrikam.Trading.Exchange.Client/
   ├── Binance/        ← если > 30 файлов или специфичные nuget
   ├── OKX/
   ├── Bybit/
   └── ...
```

Критерий выноса: > 30 файлов с собственной структурой; специфичные nuget;
независимый цикл релиза; можно отключить/заменить без влияния.

---

## 3. Testing structure — `tests/unit/` + `tests/integration/`

> ⚠️ **Phase change (2026-06-29).** Repo switched from integration-first to
> unit-first. Unit tests are primary; integration tests owned by separate
> team, live under `tests/integration/`.
>
> **Active scope (this team):** `tests/unit/` — Domain aggregates, value
> objects, CQRS handlers (mocked deps), validators, pure functions, filter
> DSL, query builders, contributors. 70% line coverage target.
>
> **Deferred scope (integration team):** `tests/integration/` — repositories,
> HTTP endpoints, hosted workers, cross-service flows. The Testcontainers /
> WebApplicationFactory / Respawn stack stays in `testing-integration.md`
> as reference for that team.

Физическое разделение по категориям (не плоско). Категория дублируется и
в имени проекта, и в под-папке.

```
tests/
├── unit/                                      # active scope (this team)
│   ├── Fabrikam.Trading.Pattern.Arbitrage.Unit.SpreadCalculation/
│   ├── Fabrikam.Trading.Pattern.Arbitrage.Unit.RiskManagement/
│   └── Acme.Shop.Architecture.Tests/          # один на весь solution (reflection, no IO)
└── integration/                               # separate team owns this
    ├── Fabrikam.Trading.Pattern.Arbitrage.Integration.Execution/
    ├── Acme.Shop.Api.Public.Integration.Health/
    └── Acme.Shop.Testing/                     # shared infra (PostgresFixture, WebAppFactory)
```

`tests/` — множественное число. НЕ `test/`.
`Architecture.Tests` живёт в `unit/` — это reflection/convention тесты без I/O.
`*.Testing` (shared integration infra) живёт в `integration/` — хелпер-библиотека
**для** integration-тестов.

### Нейминг

```
<SourceProject>.<TestKind>[.<Feature>]
```

- `SourceProject` — обязательно.
- `TestKind` — `Unit` | `Integration` | `Benchmarks`, обязательно.
- `Feature` — опционально, если у src-проекта **ровно один** тест-проект
  данного типа. Если появляется второй — оба обязаны иметь Feature.

Разделять тест-проекты когда: > 30 файлов и логически делится; разные
dependencies (Testcontainers vs нет); разные команды.

### TestKind

| TestKind | Когда |
|----------|-------|
| `Unit` | Моки, in-memory, < 100ms каждый |
| `Integration` | Реальные зависимости: БД через Testcontainers, HTTP через `WebApplicationFactory` |
| `Benchmarks` | BenchmarkDotNet |

`Architecture.Tests` — один на весь solution. Правила слоёв, нейминга,
размещения интерфейсов и моделей.

❌ Не должно быть: тестов внутри src, папки `test/` (ед. число), тест-проектов
лежащих на прямо в `tests/`, одного тест-проекта на несколько src
(исключение — `Architecture.Tests`).

---

## Связанные правила

- `project-layers.md` — обзор слоёв
- `project-naming-and-setup.md` — naming, decision tree
- `testing-stack-and-pyramid.md` — test stack
- `testing-unit.md` — unit-тесты подробно
- `testing-integration.md` — integration-тесты подробно