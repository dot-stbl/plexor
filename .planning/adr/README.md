# Architecture Decision Records (ADR)

Каждый ADR фиксирует **одно значимое архитектурное решение**:
context (что было), decision (что выбрали), rationale (почему),
consequences (что это означает для проекта), alternatives considered.

## Соглашения

- **Именование**: `NNNN-kebab-case-title.md` (sequential, начинаем с 0001)
- **Numbering**: никогда не переиспользуем номер — даже superseded ADR'ы
  остаются с оригинальным номером
- **Status**: `Proposed` → `Accepted` (или `Rejected`) → опционально
  `Superseded by ADR-NNNN`
- **Author**: автор ADR (человек, принявший решение)
- **Cross-linking**: каждый ADR ссылается на релевантные docs
  (`architecture.md`, `modules.md`, и т.п.)

## Когда писать ADR

ADR пишется **до** кода когда решение:

- Затрагивает ≥2 модуля или уровней архитектуры
- Меняет публичный API или контракт между модулями
- Меняет deploy topology (microservices, separate binaries)
- Меняет data model или migration strategy
- Меняет security model или auth flow
- Отменяет / пересматривает предыдущий ADR

**Не** пишется ADR для:

- Выбора library внутри одного модуля (просто используй)
- Bug fix (PR description достаточно)
- Refactor без изменения публичного API
- Naming/стиль решения (правила в `.agents/rules/`)

## Index

| ADR | Status | Title | Date |
|---|---|---|---|
| [ADR-0001](0001-selective-decomposition.md) | Accepted | Selective Decomposition — planned extraction of high-load modules | 2026-07-12 |
