---
description: когда и как прогонять build+verify на backend (.net) и frontend (react/ts), какие ошибки недопустимы перед коммитом
globs: ["**/*.cs", "**/*.csproj", "**/*.sln", "**/*.slnx", "**/*.ts", "**/*.tsx", "**/package.json", "**/tsconfig*.json", "**/vite.config.*"]
priority: high
interactive: false
always: true
---

# Build & Verification

Это правило описывает, **когда** и **как** запускать сборку backend (.NET) и
frontend (React/TypeScript), какие состояния считаются failure, и что делать
с pre-existing ошибками в незатронутых файлах.

## Единственная команда верификации backend

> **`dotnet build console.x.slnx -c Debug`** — это ВЕСЬ гейт. Одна команда,
> одинаковая на всех платформах, та же, что в CI. Для *верификации* не
> выдумывай свои наборы флагов — только `dotnet build`. Для *починки* drift
> есть отдельная команда (`dotnet format console.x.slnx --severity hidden`,
> см. ниже) — не путай их.

`dotnet build` делает три вещи за один прогон:

1. **Компиляция.**
2. **Анализаторы** (CA / RCS / MA / VSTHRD / IDE) — через
   `EnforceCodeStyleInBuild=true` + `TreatWarningsAsErrors=true` в
   `Directory.Build.props`. Любой `warning` = ошибка сборки.
3. **Format-check** — target `VerifyFormatOnBuild` (в
   `src/build/Hybrid.Build.Tools/Hybrid.Build.Tools.targets`) запускает
   `dotnet format --verify-no-changes --severity hidden` **один раз** на
   solution-сборку, ДО компиляции остальных проектов. **Format drift = build
   FAILS** (жёсткий гейт).

Зелёный `dotnet build` ⇒ код соответствует стандарту. Точка. Это убирает
ситуацию «локально прошло, CI красный»: CI гоняет ту же команду.

**Важно:** только **solution**-сборка (`console.x.slnx`). При сборке одного
проекта (`dotnet build src/foo/foo.csproj`) `Hybrid.Build.Tools` не попадает
в граф → format-check не запускается.

Эскейпы (только для inner-loop, не для «готово»):

```bash
dotnet build console.x.slnx -c Debug -p:DisableFormatOnBuild=true        # пропустить format-гейт
dotnet build console.x.slnx -c Debug -p:FormatOnBuildTreatAsWarning=true # drift как warning, не error
```

Починить format drift: **`dotnet format console.x.slnx --severity hidden`**.
Флаг `--severity hidden` **обязателен**: гейт верифицирует именно на этом
уровне (`VerifyFormatOnBuild` → `dotnet format --verify-no-changes --severity
hidden`), а голый `dotnet format` идёт на дефолтном `--severity warn` и **не
чинит** правила ниже `warning` (silent/suggestion — например, `IDE0320`,
`IDE0305`, `RCS1161`). Без флага `dotnet format` отработает «вхолостую», а
гейт продолжит падать на тех же violations.

**Только whitespace (быстрый фикс):** если `dotnet format --severity hidden`
застревает на analyzer errors (MA0025, RCS1163) и не доходит до whitespace
cleanup, используй сабкоманду:

```bash
dotnet format console.x.slnx whitespace
```

Это запускает **только** whitespace-правила (пустые строки, trailing spaces,
EOL) и **не** запускает analyzer rules — не застрянет ни на чём. Проверить
без изменения: `dotnet format console.x.slnx whitespace --verify-no-changes`.

## Agent contract — Definition of Done

Перед тем как сказать «готово» — прогнать набор. Без сокращений:

```bash
# 1. Build = компиляция + анализаторы + format-гейт (единственная команда).
dotnet build console.x.slnx -c Debug

# 2. Тесты.
dotnet test tests/Hybrid.ArchitectureTests --no-build --nologo

# 3. Frontend — если затронут (см. раздел "Frontend").
cd frontend && pnpm gate          # typecheck + lint + stylelint + test
# cd frontend && pnpm build       # если менялся build-вывод (vite)
```

**Правило:** exit ≠ 0 от **любой** команды = задача **не готова**. Чинить и повторить.

**Нет pre-commit hook и нет `.sh`/`.ps1`-хелперов в `scripts/`.** Gate
обеспечивается **только в CI** (`.gitlab-ci.yml` → `checks` job →
`dotnet build console.x.slnx -c Debug` + target `VerifyFormatOnBuild` +
`test:unit` job → все `*.Unit` тесты, без `Integration`). Локально разработчик
должен сам прогнать `dotnet build console.x.slnx -c Debug` перед push —
`build` gate такой же, но enforcement только в CI. Format drift чинится через
`dotnet format console.x.slnx --severity hidden` (флаг обязателен — см. выше
«Единственная команда верификации»).

**Исключение (только горячий hotfix):** если коммит блокирует прод, а гейт
мешает — пропустить format через `-p:DisableFormatOnBuild=true` и починить в
следующем коммите. **Не норма**, оправдано только срочностью, отметить в
commit message (см. `commit-format.md`).

## Когда применять

Применяется ко **всем** нетривиальным правкам в `src/`:

| Затронутая сторона | Паттерн файлов | Что проверять |
|--------------------|----------------|---------------|
| Только BE | `**/*.cs`, `**/*.csproj`, `**/*.slnx` | `dotnet build console.x.slnx -c Debug` |
| Только FE | `**/*.ts`, `**/*.tsx`, `**/package.json`, `**/tsconfig*.json`, `**/vite.config.*` | FE build |
| Обе стороны | mix of above | **Оба** билда (см. Dual-Build Rule) |
| OpenAPI / контроллеры API | `**/*.cs` с `[ApiController]` / `**/openapi.v1.json` | BE + регенерация FE API client |

**Тривиальные правки** (опечатки в markdown, переименование файла) — полный
прогон не требуется, но перед коммитом всё равно проверить сторону,
к которой относится изменение.

**Не применяется** к:
- Правкам только в `.agents/docs/`, `.planning/`, `**/*.md`
- Правкам только в `frontend/apps/console/src/shared/api/generated/` (auto-generated через openapi-typescript)
- Правкам только в `src/feature/*/Generated/` (Mapperly / source-gen)
- Правкам только в `**/Migrations/**/*.cs` (EF Core design-time artifacts — hand-edits are overwritten on `migrations add`; excluded from format-gate + analyzer style rules via a `[**/Migrations/*.cs]` block in `.editorconfig`)

## Что ловит build, чего не ловит «голый компилятор»

`VerifyFormatOnBuild` (`dotnet format --severity hidden`, самый permissive
уровень) внутри build ловит то, что анализаторы при компиляции в принципе
не покрывают:

- **Whitespace** — BOM, trailing-whitespace, EOL, пустые строки между членами.
- Style-rules с severity ниже `warning` (`silent`/`suggestion`), у которых
  есть auto-fix.

Поэтому отдельный `dotnet format --verify` запускать не нужно — он уже внутри
`dotnet build`.

**Глобально suppressed** (см. `Directory.Build.props`):
- `RMG012` (Mapperly), `CS1573` (param comment в partial-методах).

Все остальные diagnostics — **починить до коммита**. Не отключать анализаторы,
не править `.editorconfig` ради одного warning (см. `coding/ANALYZERS.md`).

## Frontend

FE-монорепо живёт в `frontend/` (pnpm@9.15 + turbo + React 19 + Vite). PM — **pnpm**,
не bun (см. `frontend/CLAUDE.md`).

```bash
cd frontend && pnpm gate          # каноничный FE-гейт: typecheck + lint + stylelint + test
cd frontend && pnpm build         # turbo run build (vite) — если менялся build-вывод
cd frontend && pnpm gate:full     # + e2e + build (полный verify)
```

`pnpm typecheck` = `tsc --noEmit` (через turbo); `pnpm build` = `vite build`.

**Требования к выходу:**
- Exit code = `0`, **0 TypeScript errors**, eslint/stylelint чисто.
- Vite warnings (chunk size > 500kb, dynamic import hints) — допустимы.

**Дополнительно (по ситуации):**

| Что | Команда |
|-----|---------|
| Typecheck | `cd frontend && pnpm typecheck` |
| Линт FE (eslint) | `cd frontend && pnpm lint` |
| Тесты FE (vitest) | `cd frontend && pnpm test` |
| E2E (playwright) | `cd frontend && pnpm test:e2e` |
| Regen API client | `cd frontend && pnpm codegen` (openapi-typescript из `contracts/openapi.yaml`) |

## Dual-Build Rule

Если задача затрагивает **обе стороны** (новый endpoint + страница, смена
контракта API, схема БД, отражающаяся в FE) — прогнать **обе** сборки:

```bash
# 1. Backend (компиляция + анализаторы + format-гейт).
dotnet build console.x.slnx -c Debug

# 2. Frontend.
cd frontend && pnpm gate

# 3. Опционально: регенерация API client, если менялись контроллеры.
cd frontend && pnpm codegen
cd frontend && pnpm build
```

**Обе** должны дать exit 0. Недопустимо «FE компилируется, BE потом починю».

## Pre-existing errors

Ошибки в **незатронутых** файлах — **не** повод отложить:

| Ситуация | Что делать |
|----------|------------|
| Warning/Error в **затронутом** файле | ✅ Починить в рамках задачи |
| Warning/Error в **незатронутом** файле | ✅ Починить (цель — `0 warnings`) |
| Техдолг в `.planning/BACKEND-ISSUES.md` | ⚠️ Можно отложить, **отметить в commit message** |

❌ **Запрещено:**
- `// @ts-ignore` / `// eslint-disable-next-line` без обоснования.
- `dotnet_diagnostic.* = none` в `.editorconfig` для подавления warning (нужен
  owner-approval — см. `coding/ANALYZERS.md`).
- `#pragma warning disable` без `restore` и без комментария «почему».

## Перед коммитом — чеклист

```
1. git status / git diff --stat
   ↓
2. По списку файлов определить затронутые стороны (BE / FE / обе)
   ↓
3. Если BE  → dotnet build console.x.slnx -c Debug
   Если FE  → cd frontend && pnpm gate
   Если обе → ОБА
   ↓
4. Все exit 0?  ── Нет → починить, goto 3
                  ↓ Да
5. git add + commit
```

## Good / Bad

```bash
# ✅ Correct — затронут только BE, одна команда ловит всё (compile + analyzers + format)
$ dotnet build console.x.slnx -c Debug
 ... Build succeeded. 0 Warning(s) 0 Error(s)
$ git commit -m "[hybrid](api): add Bar endpoint"
```

```bash
# ❌ Wrong — затронуты обе стороны, проверен только BE
$ dotnet build console.x.slnx -c Debug   # OK
$ git commit -m "..."                    # FE не проверен — TS-ошибка уйдёт в CI
```

```bash
# ❌ Wrong — свой набор команд вместо стандарта
$ dotnet format ... --severity warn      # не тот severity, не та проверка
$ dotnet build src/foo/foo.csproj        # per-project → format-гейт не сработал
# Стандарт ОДИН: dotnet build console.x.slnx -c Debug
```

## Связанные правила и файлы

- `.agents/rules/coding/ANALYZERS.md` — анализаторы, политика severity, owner-approval
- `.agents/rules/coding/CODING-RULES.md` — code style
- `.agents/rules/coding/TESTING-RULES.md` — запуск тестов
- `.editorconfig` + `tests/.editorconfig` — severity правил (прод / тесты)
- `Directory.Build.props` — глобальные suppressed warnings, `TreatWarningsAsErrors`
- `src/build/Hybrid.Build.Tools/Hybrid.Build.Tools.targets` — `VerifyFormatOnBuild` гейт
- `.planning/BACKEND-ISSUES.md` — зафиксированный техдолг