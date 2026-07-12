# ADR-0001: Selective Decomposition — planned extraction of high-load modules

- **Status:** Accepted
- **Date:** 2026-07-12
- **Decision driver:** bradw (owner)

## Context

Plexor — modular monolith в MVP: один `Plexor.Host` бинарь со всеми
модулями. Это осознанный выбор — single binary, single deployment.

Со временем часть модулей вырастет в scale-проблемы для control plane.
Три кандидата на extraction в Phase 2+: **Audit** (write-heavy,
7-yr retention), **Telemetry** (OTel collector, cardinality spikes),
**Network** (SDN control plane, state explosion).

## Decision

1. **MVP остаётся modular monolith** — все модули в `Plexor.Host`.
2. **Extraction-ready дизайн с первого дня** — каждый модуль можно
   вынести в отдельный бинарь без refactor. Конкретные правила —
   в [modules.md §Extraction Tier](../../.agents/docs/modules.md#extraction-tier).
3. **Phase 2+ extraction** трёх модулей в отдельные бинарь
   (`Plexor.Audit.Host`, `Plexor.Telemetry.Host`, `Plexor.Network.Host`),
   когда метрики покажут реальный bottleneck. Триггеры уточним в
   момент extraction, не заранее.
4. **Никаких других extraction candidates** — Tenants, Identity,
   Compute, Storage, Billing, Marketplace остаются в монолите.

## Rationale

- **Не 20+ микросервисов** (OpenStack-style): операционный overhead
  убивает маленькие команды. Counter-evidence: GitHub, Shopify,
  Stack Overflow — все обслуживали миллиарды на монолите.
- **Не "пишем как монолит, рефакторим при extraction"**: extraction
  предсказуем (мы знаем какие модули горячие), границу дешевле
  провести сразу, чем потом.
- **Не "выделяем сразу в Phase 1"**: нет измеренной нагрузки,
  extraction — это operational cost, не virtue.

## Consequences

**Positive**:
- MVP = single binary, fast delivery, `plx init` за минуты
- Phase 2+ extraction = deploy change, не refactor
- Independent scaling горячих модулей позже

**Negative**:
- Upfront cost: каждый модуль имеет `IPort` interface в
  `Plexor.Shared.Contracts` даже когда используется in-process
- DB schemas требуют дисциплины (уже enforced)
- Outbox volume добавляет в control plane DB

## References

- [architecture.md §Decomposition strategy](../../.agents/docs/architecture.md)
- [modules.md §Extraction Tier](../../.agents/docs/modules.md#extraction-tier)
- [scope.md §Принцип](../../.agents/docs/scope.md)