---
description: формат и иерархия правил в проекте — где что лежит, как грузится, как добавлять новое
always: true
---

# Формат правил

В проекте **две** системы правил. Этот файл — карта между ними: что куда класть, как грузится, как не дублировать.

Каноническое дерево правил — `.agents/rules/`. `.claude/rules/` — **тонкие указатели** на канон, без дублирования содержания.

## 1. Где что лежит

### `.agents/rules/<category>/*.md` — канонические правила

Файлы с soly frontmatter. Грузятся:
- по `globs` (применяется к указанным файлам),
- по `always: true` (всегда в контексте).

Используются для:
- code-style (C# правила, conventions),
- process (workflow, build, commits),
- тестирования (когда писать, какой стэк),
- архитектуры (слои, зависимости),
- security, performance — по необходимости.

### `.claude/rules/*.md` — указатели для batch-агентов

Тонкие файлы-ссылки на `.agents/rules/`. **Не дублируют** содержание.
Грузятся всеми агентами одинаково.

> **Hard rule: дублирование содержания между `.agents/` и `.claude/` запрещено.**
> Если правило развёрнуто в `.agents/rules/coding/X.md` — в `.claude/rules/`
> только ссылка, не копия.

## 2. Frontmatter — REQUIRED формат

```yaml
---
description: one-line lowercase — что это правило ограничивает
globs: ["**/*.py", "**/requirements.txt"]   # опционально
priority: high | medium | low                # опционально
interactive: false                           # true = только для интерактивного LLM
always: false                                # true = обходить glob-проверку
---
```

**`description` — всегда lowercase, разделитель `-` или `.`. Без исключений.**

Это касается всей meta-информации в проекте: frontmatter-поля, имена тегов
OTel (`command.name`), имён метрик (`cqrs.command.duration`), datasource
name в Grafana. Capitalization оправдана только convention'ом языка/платформы
(C# class names, JSON `schemaVersion`).

## 3. Иерархия (от высшего приоритета к низшему)

1. `.agents/rules.local/` (per-project, gitignored, личные overrides)
2. `.agents/rules/` (per-project, коммитится в репу)
3. `.claude/rules/` (коммитится, грузится всеми агентами)
4. `~/.soly/rules/` (user-global)
5. `~/.claude/rules/` (user-global)

При коллизии — выигрывает правило с **меньшим** номером.

## 4. Текущая структура `.agents/rules/coding/`

```
coding/
├── naming-and-types.md           # CODING: naming, sealed, record vs class, type refs
├── constructors-and-fields.md    # CODING: primary ctor, fields, constants
├── code-shape.md                 # CODING: pattern matching, var, ns, braces, comments, collections, no #region, no ThrowIfNull
├── class-layout-and-tooling.md   # CODING: XML docs, model placement, required tooling
├── async-and-tasks.md            # CODING: async/await, ConfigureAwait ban
├── anti-patterns.md              # CODING: enums, records DTO, tuples, validation, FromServices, JsonSerializerOptions
├── ef-core.md                    # FRAMEWORK: queries, writes, snake_case, string length, migrations
├── entity-models.md              # FRAMEWORK: entity config, OnModelEntity, interfaces
├── api-design.md                 # FRAMEWORK: controllers, OpenAPI attrs, URL structure
├── logging.md                    # FRAMEWORK: structured logging
├── di-installer.md               # DI: installer pattern, composition root
├── di-options.md                 # DI: IOptions pattern, validation, monitor
├── di-lifetimes.md               # DI: service lifetimes, captive dependency
├── project-layers.md             # PROJECT: layers, responsibilities
├── project-naming-and-setup.md   # PROJECT: repo, slnx, decision tree, naming, new project, internal structure
├── project-deps-and-tests.md     # PROJECT: layer deps, testing structure, anti-patterns
├── testing-stack-and-pyramid.md  # TESTING: stack, decision tree
├── testing-unit.md               # TESTING: unit tests, Shouldly, NSubstitute, Builders, anti-patterns
├── testing-integration.md        # TESTING: integration tests (reference for integration team)
├── analyzers.md                  # analyzer packages wiring
└── rules-format.md               # ← этот файл
```

```
.agents/rules/
├── coding/        # (см. выше)
└── process/
    ├── build-verification.md
    ├── commit-format.md
    ├── engineering-zone-access.md
    ├── migrations.md
    └── worker-audit.md
```

## 5. Как добавить новое правило

### Большой мануал в `.agents/rules/coding/`

1. Создать `<topic>.md` рядом с существующими (lowercase, kebab-case).
2. **Обязательно** frontmatter с `description:`.
3. Body: ToC + `##` / `###` + Good / Bad примеры + Enforcement/Test line.
4. Если правило применяется всегда — добавить `always: true`.
5. Если правило узкое (только для конкретных файлов) — добавить `globs: [...]`.

### Указатель в `.claude/rules/`

1. Создать `<NN>-<topic>.md`.
2. **Только** ссылка на канон, не копия.
3. Без frontmatter — обычный markdown.

## 6. Reload

После правки `.agents/rules/` — перезапуск сессии подхватит изменения
автоматически. Для soly-aware агентов: `/rules reload`.

## Связанные правила

- `process/build-verification.md` — build gate (что ловится автоматически)
- `process/worker-audit.md` — self-audit gate (что проверяет LLM вручную)
- `process/engineering-zone-access.md` — какие файлы правил LLM может редактировать