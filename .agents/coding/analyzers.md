---
description: c# analyzer packages — roslynator, meziantou, netanalyzers wiring. как они подключены, что делать при новых warnings.
globs: ["**/*.csproj", "**/Directory.Build.props", "**/Directory.Packages.props", "**/.editorconfig"]
always: true
---

# Analyzer packages — cheatsheet

C#-анализаторы подключены **централизованно** в `Directory.Build.props`.
CPM выключен (см. комментарий в `Directory.Packages.props`) — версии пакетов
явные.

## Что встроено / настроено

| Пакет | Статус | Конфиг |
|-------|--------|--------|
| `Microsoft.CodeAnalysis.NetAnalyzers` (CA) | встроен в .NET SDK | `<AnalysisLevel>latest</AnalysisLevel>` в `Directory.Build.props` |
| `Microsoft.VisualStudio.Threading.Analyzers` (VSTHRD) | встроен в SDK | используется через analyzer rules |
| `Meziantou.Analyzer` (MA) | подключён | `Directory.Packages.props` + `Directory.Build.props` |
| `Roslynutor.Analyzers` (RCS) | подключён | `Directory.Packages.props` + `Directory.Build.props` |

Build-флаги в `Directory.Build.props`:
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — warnings = errors
- `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` — style rules в build
- `<AnalysisLevel>latest</AnalysisLevel>` — последние CA-правила

Severity конкретных правил — в `.editorconfig` (~50 записей).

## Когда подключать analyzer к одному проекту

Если analyzer нужен **только** одному проекту (не глобально), в его `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Roslynator.Analyzers" Version="4.*" PrivateAssets="all" />
</ItemGroup>
```

`PrivateAssets="all"` обязателен — иначе analyzer утечёт в runtime-зависимости.

## Что делать при новом warning

**Приоритет — жёсткий, в этом порядке:**

1. **Починить код.** Warnings = code smell. Build падает.
2. **Локально подавить** через `#pragma warning disable RULE // <почему>` +
   `#pragma warning restore RULE`. Разрешено без одобрения владельца —
   ограничено конкретным местом.
3. **Запросить одобрение** на глобальное ослабление severity через `.editorconfig`.
   Ждать `ok` владельца. После одобрения — запись в `.planning/BACKEND-ISSUES.md`
   со ссылкой на чат.

## Hard rule: `.editorconfig` менять только с одобрения владельца

Любые правки `.editorconfig` (severity override, new rule, `[*.cs]` block,
reorganization) требуют **явного одобрения владельца в чате** перед коммитом.
Agent **не** вносит такие правки самостоятельно.

**Почему:** `.editorconfig` — single source of truth для code style на весь
solution. Локально-мотивированное `severity = none` ради обхода текущего
затруднения накапливается (1 правило сегодня, 5 через месяц). Каждое
ослабление — осознанное проектное решение, не convenience-переключатель.

**Запрещено:**

- ❌ Тихо ослаблять severity при первом столкновении.
- ❌ Добавлять `[*.cs]` блок для обхода одного правила.
- ❌ Коммитить `.editorconfig` change в одном PR с другим функционалом — маскировка.
- ❌ Говорить «это просто стиль, неважно» — стиль важен.
- ❌ Добавлять `<NoWarn>` в csproj без записи в baseline-issue.

**Разрешено без одобрения:**

- ✅ Любые правки `*.cs` (code-level fixes).
- ✅ `#pragma warning disable` в конкретном месте с обоснованием.
- ✅ Усиление (добавление нового правила с severity = `error`).
- ✅ Запись в `.planning/BACKEND-ISSUES.md` со ссылкой на issue.

## Чеклист: добавить новый analyzer package

1. `<PackageVersion Include="..." Version="..." />` в `Directory.Packages.props`.
2. `<PackageReference Include="..." PrivateAssets="all" />` в `Directory.Build.props` (для всех) или в `.csproj` (для одного).
3. `dotnet build console.x.slnx -c Debug` — посмотреть новые warnings.
4. Разобрать warnings: починить / подавить с обоснованием / baseline.
5. Отдельный коммит `[hybrid](meta/deps): add <package> analyzer`.

## Связанные правила

- `.editorconfig` — severity каждого правила
- `process/build-verification.md` — build gate (компиляция + analyzers + format)
- `process/worker-audit.md` — self-audit gate перед коммитом