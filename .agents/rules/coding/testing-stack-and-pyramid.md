---
description: test stack — xunit v3 + shouldly + nsubstitute + testcontainers + respawn. когда unit, когда integration
globs: ["tests/**/*.cs", "tests/**/*.csproj"]
always: true
---

# Test stack & pyramid

Этот файл — стэк и решение когда unit vs integration. Unit-тесты подробно —
в `testing-unit.md`. Integration-тесты подробно — в `testing-integration.md`.

> ⚠️ **Phase change (2026-06-29).** Repo switched from integration-first
> to unit-first. Unit tests are primary; integration tests owned by
> separate team, live under `tests/integration/`.
>
> **Active scope (this team):** `tests/unit/` — Domain aggregates, value
> objects, CQRS handlers (mocked deps), validators, pure functions, filter
> DSL, query builders, contributors. 70% line coverage target.
>
> **Deferred scope (integration team):** `tests/integration/` —
> repositories, HTTP endpoints, hosted workers, cross-service flows. The
> Testcontainers / `WebApplicationFactory` / Respawn stack stays in
> `testing-integration.md` as reference for that team.
>
> Decision recorded in `.agents/STATE.md` (2026-06-29).

---

## 1. Stack — ФИКСИРОВАННЫЙ

| Назначение | Библиотека | Версия |
|------------|------------|--------|
| Test framework | **xUnit v3** | 1.x+ |
| Assertions | **Shouldly** | 4.x+ |
| Mocking | **NSubstitute** | 5.x+ |
| Container infrastructure | **Testcontainers** | 4.x+ |
| Web API testing | **Microsoft.AspNetCore.Mvc.Testing** | matches .NET version |
| DB cleanup between tests | **Respawn** | 6.x+ |
| Fake data generation | **Bogus** | 35.x+ |
| Coverage | **Coverlet.collector** | 6.x+ |

### Почему этот стэк

- **xUnit v3** — на `Microsoft.Testing.Platform`, не `VSTest`; быстрее discovery.
- **Shouldly** вместо FluentAssertions — FluentAssertions 8.0+ коммерческая (Xceed).
- **NSubstitute** вместо Moq — Moq в 2023 встроил SponsorLink (сбор email).
- **Testcontainers** — реальная Postgres/Redis в Docker.
- **Respawn** — быстрая очистка БД через TRUNCATE.

### Исключение из CODING-RULES для тестов

**`IAsyncLifetime.InitializeAsync()` / `DisposeAsync()` не принимают
`CancellationToken`.** Это override интерфейса xUnit, сигнатура зафиксирована
библиотекой. Правило "CancellationToken последним" применяется к нашим
методам, не к override чужих интерфейсов.

```csharp
// ✅ Корректно — это override IAsyncLifetime
public async Task InitializeAsync()
{
    await Container.StartAsync();
}

// ✅ Наши собственные методы — с CancellationToken
public async Task ResetAsync(CancellationToken cancellationToken = default)
{
    await respawner.ResetAsync(connection, cancellationToken);
}
```

В остальном тестовый код подчиняется тем же правилам: `var`, file-scoped
namespaces, braces везде, осмысленные lambda-имена, structured logging,
`is null` вместо `== null`.

### Csproj шаблон тест-проекта

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="1.*" />
    <PackageReference Include="Shouldly" Version="4.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="coverlet.collector" Version="6.*" />
  </ItemGroup>

  <!-- Только для integration projects: -->
  <ItemGroup Condition="'$(IsIntegrationTest)' == 'true'">
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.*" />
    <PackageReference Include="Respawn" Version="6.*" />
  </ItemGroup>

</Project>
```

Общие пакеты (xUnit, Shouldly, NSubstitute, coverlet) выносятся в
`Directory.Build.props` для всех проектов в `tests/` через
`Condition="'$(IsTestProject)' == 'true'"`.

---

## 2. Decision tree — когда unit, когда integration

```
Что тестируем?

├── Калькулятор / парсер / форматтер / валидатор?
│   └── UNIT (чистая функция)
│
├── Стратегия / алгоритм / pattern matching с многими ветками?
│   └── UNIT с [Theory] + InlineData (или MemberData)
│
├── Repository / EF query / SQL?
│   └── INTEGRATION с Testcontainers (integration team)
│
├── HTTP endpoint?
│   └── INTEGRATION через WebApplicationFactory (integration team)
│
├── Сервис, который ходит во внешний API?
│   ├── Hot path (логика обработки ответа)        → UNIT с моком клиента
│   └── Сам клиент к API                          → CONTRACT tests (integration team)
│
├── Workflow из нескольких сервисов?
│   └── INTEGRATION end-to-end (integration team)
│
└── HostedService / BackgroundService?
    └── INTEGRATION (integration team)
```

### Что НЕ тестируем вообще

- DTO / Records без логики (только `init`-properties) — нечего тестировать.
- EF Core entities (если только в них нет custom-методов).
- Auto-mapped профили AutoMapper/Mapperly — если простой 1:1 mapping.
- Microsoft / NuGet библиотеки.
- Один-в-один обёртки над сторонним API без логики.

---

## 3. Coverage thresholds — 70% line для unit-проектов

В `Directory.Build.props` тест-проектов:

```xml
<PropertyGroup>
  <CollectCoverage>true</CollectCoverage>
  <CoverletOutputFormat>cobertura,opencover</CoverletOutputFormat>
  <Threshold>70</Threshold>
  <ThresholdType>line</ThresholdType>
  <ThresholdStat>total</ThresholdStat>
</PropertyGroup>
```

**70% line coverage** для `tests/unit/`. Не 80%+ — ведёт к бессмысленным
тестам. Integration — без threshold (отдельная команда).

Что исключаем:

```xml
<PropertyGroup>
  <ExcludeByFile>
    **/Program.cs,
    **/*.Designer.cs,
    **/Migrations/**/*.cs,
    **/Generated/**/*.cs
  </ExcludeByFile>
  <Exclude>[*.Tests]*,[*.Benchmarks]*</Exclude>
</PropertyGroup>
```

---

## Связанные правила

- `testing-unit.md` — unit-тесты подробно
- `testing-integration.md` — integration-тесты подробно (reference для integration team)
- `project-deps-and-tests.md` — testing structure, naming