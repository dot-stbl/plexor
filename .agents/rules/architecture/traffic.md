---
description: plexor traffic layer — http/json rest default, grpc opt-in for inter-cluster. браузер → host = http/json. service-to-service = http по умолчанию, gRPC при триггерах.
globs: ["**/*.cs"]
always: true
---

# Traffic layer (HTTP/JSON vs gRPC)

## Правило по умолчанию

**HTTP/JSON REST** — default для всего в Plexor. Это включает:

- Браузер ↔ Host (UI /api/v1/*, kubb codegen, MSW моки)
- Host ↔ NodeAgent (Refit-typed client, уже реализовано)
- Host ↔ AuditService (POST /events, GET /events с фильтрами)
- Host ↔ внешние IdP, storage, etc. (Keycloak, S3-compatible)
- Host ↔ другие Plexor instances (inter-cluster)

**Один стек, один toolchain.** OpenAPI source-gen (Microsoft.AspNetCore.OpenApi), Refit клиенты, kubb codegen → TypeScript types. Всё описано в одном `plexor.openapi.yaml`.

## Когда gRPC РЕАЛЬНО нужен (триггеры)

Не выбираем gRPC "потому что быстрее" или "потому что модно". Включаем **только** при выполнении одного из:

1. **Server-streaming required** — live event log (audit feed, k8s node status, etc.) с минимальной задержкой
2. **Polyglot client** — потребитель на не-.NET языке (Go, Rust, Python service) и контракт должен быть строгим .proto
3. **Throughput / latency bottleneck** — профайлер показывает > 100ms p99 на cross-service HTTP-вызове И объём > 1000 req/sec
4. **Inter-cluster** — два разных Plexor instance'а общаются между собой. gRPC даёт строгий .proto, bi-di streaming, лучше чем REST для control plane

**В v0.x** триггеры #1, #2 не сработают. Триггер #3 нерелевантен — нагрузки мизерные. Триггер #4 потенциально релевантен если строим **multi-cluster federation** (Phase 2+), но в v0.1 Plexor = **single-cluster self-hosted**.

→ **gRPC не в v0.x.** Если в будущем — отдельный .proto репо + Grpc.Tools + Grpc.AspNetCore.

## Anti-patterns

- ❌ **gRPC для browser-facing API** — grpc-web имеет friction, и UI-team не сможет использовать fetch/axios. Браузер всегда HTTP/JSON.
- ❌ **gRPC для low-volume admin operations** — `plx cluster show` через gRPC только потому что "может пригодиться" — overengineering.
- ❌ **Mixed HTTP+gRPC внутри одного микросервиса** — host предлагает 4 REST endpoint'а + 1 gRPC. Confusing for clients. Один protocol per service.
- ❌ **.proto + JSON одновременно** — либо .proto, либо OpenAPI. Двойной контракт = double maintenance.

## Когда пересматривать

- Профайлер показывает HTTP p99 > 100ms на cross-service
- Inter-cluster federation в roadmap
- Потребитель на не-.NET языке

До тех пор — **HTTP/JSON REST везде, OpenAPI как single source of truth**.

## Self-audit

```bash
# Найти gRPC-пакеты в коде — должны быть пусто для v0.x
rg -n "Grpc\.|GrpcCore|protobuf" src/ --type cs

# Должен быть только Refit
rg -n "Refit\.|IApiResponse" src/ --type cs
```
