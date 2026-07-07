---
description: Mandatory self-audit gate for worker subagents — run after writing code, before git commit. Catches analyzer violations, cross-checks against loaded rules, fills rule gaps via analyzer-coach skill.
priority: high
---

# Worker self-audit gate

После того как ты написал код, и **до** `git commit` — ОБЯЗАТЕЛЬНО прогон
этого gate. Цель: поймать нарушения правил которые ты внёс, и
обнаружить пробелы где существующих правил не хватает.

Skipping = полагаться на CI чтобы поймать то что ты пропустил. Это
противоположно self-verification.

## Шаги (по порядку)

### 1. Mechanical: build + analyzer warnings

```bash
dotnet build console.x.slnx -c Debug
```

**Любой warning = failure.** В этом проекте `TreatWarningsAsErrors=true`
глобально (см. `Directory.Build.props`), так что build падает
автоматически. Та же команда прогоняет format-гейт (`VerifyFormatOnBuild`):
**format drift тоже валит build** (см. `process/build-verification.md`).

**Чини код, не подавляй warning.** Документированный escape hatch —
только с baseline-issue (см. `coding/ANALYZERS.md` §"Что делать при новых
warnings"):

- `#pragma warning disable` — только с комментарием-обоснованием + restore
- `dotnet_diagnostic.X.severity = none` — **запрещено** (это отключение
  правила, а не enforcement)
- `<NoWarn>` в csproj — только с записью в baseline-issue

### 2. Convention: cross-check diff против загруженных rules

Ты уже имеешь в контексте эти rules (load order ниже на случай если
не помнишь что у тебя в system prompt):

- `coding/CODING-RULES.md` — naming, primary ctor, sealed, async, var, braces
- `coding/FRAMEWORK-RULES.md` — EF Core, ASP.NET Core, MS.Ext.Logging, Gridify
- `coding/ANALYZERS.md` — analyzer packages, severity в editorconfig
- `coding/PROJECT-STRUCTURE.md` — слои, нейминг, layout
- `coding/TESTING-RULES.md` — xUnit + Shouldly + NSubstitute + Testcontainers
- `process/build-verification.md` — 0 warnings перед commit
- `process/commit-format.md` — Conventional Commits 1.0.0

**Пройдись по diff'у и для каждого релевантного rule проверь
compliance.** «Я вроде нормально написал» — это не проверка. Реально
прочитай правило, найди в diff'е место, убедись что соответствует.

Частые точки внимания:
- Public методы без `<summary>` (CODING-RULES §10)
- Braces без скобок (CODING-RULES §9)
- Expression-bodied метод вместо block body `{ }` — запрещён, IDE0022 (CODING-RULES §9)
- Async метод без `Async` суффикса (CODING-RULES §1, VSTHRD200)
- `_privateField` (CODING-RULES §1 — convention)
- Explicit ctor + `private readonly _field` когда хватает primary ctor (CODING-RULES §3)
- `private readonly`-поле, инициализированное из параметра primary ctor (с подчёркиванием ИЛИ без) — дублирующее хранение, удалить, использовать параметр (CODING-RULES §3)
- Несколько public-классов в одном файле (CODING-RULES §14 — one-class-per-file)
- `var` vs explicit type (CODING-RULES §7)
- `IReadOnlyCollection` vs `List` в public API (CODING-RULES §12)
- **EF string-свойство без лимита** (FRAMEWORK-RULES.md §1 "String properties
  must have explicit length") — каждая `.Property(x => x.SomeString)` в
  `IEntityTypeConfiguration` обязана иметь `.HasMaxLength(n)` ЛИБО
  `.HasColumnType("jsonb"/"char(N)")`. Bare string → unbounded `text` column.
- **Assign + nullcheck на раздельных строках** (CODING-RULES §5) —
  ловит `VerifyAntiPatternsOnBuild` target, но только `is null`/`== null` форму.
  Build-гейт `is not null` форму **пропускает** — проверяй вручную:
  - Guard clause: `var x = await ...; if (x is null) { return; }` →
    `if (await ... is not { } x) { return; }`
  - Get-or-return-existing: `var x = await ...; if (x is not null) { return x; }` →
    `if (await ... is { } x) { return x; }`
- **`ArgumentNullException.ThrowIfNull`** (CODING-RULES §19) в nullable-enabled
  контексте — дубль статического контракта компилятора. Non-nullable
  параметр уже обеспечен на call-site; `ThrowIfNull` внутри = мусор.
  Оставлять только `ThrowIfNullOrEmpty` / `ThrowIfNullOrWhiteSpace`
  (empty-check — бизнес-логика) и boundary-вызовы (reflection/interop) с
  комментарием.
- **`#region` директивы** (CODING-RULES §18) запрещены — прячут структуру,
  поощряют большие файлы, шумят в diff. Если хочется region
  «Constructors»/«Helpers» → вынести в отдельный тип или упорядочить
  members (CODING-RULES §14).
- **Enum-anti-patterns** (CODING-RULES §20) — enum с неявными данными
  (`FrequencyCapPeriod` + switch на длительность), enum-as-command
  (`CampaignStatusAction` switch на 6 case), switch-sprawl с 4+ ветками
  логики → заменить на отдельные commands или state pattern.
- **EF DbContext — snake_case** (FRAMEWORK-RULES.md §1) — каждый `.Property`
  обязан иметь `HasColumnName("snake_case")`; регистрация DbContext через
  `AddModuleDbContext<T>` (не raw `AddDbContext`).
- `throw ex;` вместо `throw;` (FRAMEWORK-RULES / analyzer-coach cookbook)
- **Record DTO в controller-файле** (CODING-RULES §21) — request/response
  records живут в `Application/Models/`, отдельный файл на тип. Никогда
  inline в controller. `grep -nE '^public (sealed )?record ' <Controller>.cs`
  должен быть пустым.
- **Tuples в public API** (CODING-RULES §22) — `(Type1, Type2)` в return
  types / parameters / properties запрещены. Заменить на record.
  `grep -nE 'public\s+\([A-Z]\w+,.*\)\s+\w+' *.cs` — должно быть пусто.
- **Validation — только FluentValidation** (CODING-RULES §23) — никаких
  приватных методов-валидаторов в controller, никаких `ModelState.AddModelError`
  если есть валидатор. Каждый модуль обязан регистрировать валидаторы через
  `AddValidatorsFromAssembly`.
- **Endpoint-specific dependencies — `[FromServices]`** (CODING-RULES §24) —
  сервис который нужен ровно 1 endpoint'у в controller, не в конструкторе.
  Через `[FromServices]` на параметре action-метода.
- Commit message format (commit-format.md)

### 3. Rule gaps: вызови `analyzer-coach` skill

Если ты нашёл в diff'е style issue, который:
- Не enforced ни одним rule (analyzer + .editorconfig + convention docs)
- Скорее всего повторится (не разовый случай)

→ **Запусти `analyzer-coach` skill.** Он предложит один из вариантов:

| Куда | Когда |
|---|---|
| `.editorconfig` (через `dotnet_diagnostic.RCS####.severity`) | Issue покрывается standard analyzer (Roslynator/CA/MA/VSTHRD) |
| `coding/CODING-RULES.md` или `coding/FRAMEWORK-RULES.md` | Convention/pattern, нет standard rule |
| **Custom analyzer** в `platform/src/generation/Acme.Shop.Code.Roslyn/` (правила `CMK####`) | Project-specific, code-level check даст больше чем convention |
| "Not analyzable" verdict | Это правда convention, обсуди в code review |

Custom analyzer path конкретно:
- Проект: `platform/src/generation/Acme.Shop.Code.Roslyn/`
- Convention: один файл на правило, sealed class, public const `DiagnosticId` = `CMK####`
- Severity задаётся в `.editorconfig` (`dotnet_diagnostic.CMK0001.severity = error`)
- Перед добавлением проверь что issue не покрыт standard analyzer'ом

Примени proposal, прогони шаг 1 ещё раз чтобы убедиться что ничего не
сломал.

### 4. Loop до чистого состояния

Если шаг 1 или 2 находит violations — fix и перепрогон обоих. **Max 3
итерации** чтобы не уйти в infinite loop на genuine conflicts. Если
после 3 итераций что-то всё ещё не проходит — опиши проблему в
completion report и попроси parent решить (не коммить с warning).

### 5. Только после этого commit

Commit только когда:
- Шаг 1 passes (0 warnings)
- Шаг 2 не нашёл violations
- Rule gaps из шага 3 либо resolved, либо явно описаны в report

**Pre-commit hook'а нет** — вся валидация в `dotnet build console.x.slnx`
(компиляция + анализаторы + format-гейт). Не обходи гейт через
`-p:DisableFormatOnBuild=true` ради «готово».

## Почему это mandatory

Цель soly'евской rule infrastructure — LLM пишет код соответствующий
project standards **автоматически**, не «стараясь». Этот gate это
enforce'ит. Skipping = мы в том же месте что и без rules: «агент
написал, CI поймал, переделываем».

## Связанные rules

- `coding/ANALYZERS.md` §"Что делать при новых warnings" — escape hatches
- `process/build-verification.md` — full verify перед "готово"
- `process/commit-format.md` — Conventional Commits

## См. также

- `~/.pi/agent/skills/analyzer-coach/SKILL.md` — skill для шага 3 (rule gaps)
- `~/.pi/agent/skills/analyzer-coach/references/cookbook.md` — топ-30 жалоб → правила
