# MCP Server — управление Plexor через Model Context Protocol

> Идея: Plexor экспонирует [Model Context Protocol](https://modelcontextprotocol.io)
> сервер, чтобы AI-агенты (Claude Code, Cursor, Windsurf, любой MCP-клиент)
> могли управлять Plexor через разговор — а не (только) через REST API + UI.
>
> Две аудитории, **один сервер**:
> 1. **Self-hosted пользователь** у себя развернул Plexor — хочет чтобы его
>    собственный Claude Code на ноуте управлял его кластером.
> 2. **Plexor-контрибьютор** разрабатывает / дебажит — хочет чтобы агент
>    помогал с fixtures, миграциями, логами локального dev-инстанса.
>
> Статус: design-anchor. Реализация — после того как осядут Compute /
> Network / Identity модули (нам нужны конкретные tools чтобы экспонировать).
> Код **не пишется** пока нет реальных модулей с реальной логикой.

---

## TL;DR

- **Plexor.Host** стартует **in-process MCP-сервер** как hosted service
  (включается флагом `Mcp:Enabled=true`, по умолчанию **off**).
- Транспорт: **stdio** (для локального агента на той же машине) **+**
  **HTTP+SSE** на отдельном порту (для удалённого агента / команды).
- Auth: **tenant API key** в `Authorization: Bearer` — тот же механизм,
  что в `Identity.ApiKey` (см. cross-service-communication rule).
- Surface: **v1 — MCP resources** (read-only, URI `plexor://vms/{id}`,
  `plexor://vms?filter=...`). **v2 — tools** для мутирующих ops
  (`create_vm`, `delete_*`). Идиоматичное MCP-разделение: read-only
  = resource, mutation = tool.
- Dev-mode: **отдельные tools** (fixtures, query_db, run_migration),
  доступные только при `PLEXOR_MCP_DEV_MODE=true`. В прод-сборке
  код dev-tools **не линкуется** (conditional `<ItemGroup>`).
- Дистрибуция: NuGet-пакет `Plexor.Mcp` (или подпапка
  `src/host/Plexor.Host/Mcp/` — решение deferred до обсуждения).

```
┌──────────────────────────────────────────────────────────────────────┐
│  AI Agent (Claude Code / Cursor / Windsurf / custom)                │
│  MCP client                                                         │
└───────────────────────────┬──────────────────────────────────────────┘
                            │  MCP (stdio  или  HTTP+SSE :9443)
                            │  Authorization: Bearer <tenant-api-key>
                            ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Plexor.Host  (control plane)                                        │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  McpServerHostedService                                        │  │
│  │  ├─ Transport: stdio / http+sse (configurable)                │  │
│  │  ├─ Auth: tenant API key → TenantContext                      │  │
│  │  ├─ Tools (read-only v1):                                     │  │
│  │  │   list_vms, get_vm, list_clusters, show_logs,             │  │
│  │  │   show_audit, list_users, get_billing_summary             │  │
│  │  └─ Tools (dev-mode, PLEXOR_MCP_DEV_MODE=true):               │  │
│  │      query_db (guarded SELECT-only), seed_fixtures,          │  │
│  │      run_migration_target, tail_node_log                      │  │
│  └────────────────────────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  Modules (Compute / Network / Storage / Identity / ...)       │  │
│  │  — tools читают те же query-services что REST controllers      │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Принципы

1. **Один сервер, две аудитории.** Self-hosted user и Plexor-разработчик
   подключаются к одному MCP endpoint. Различие — **не** в URL, а в
   **API key scope** (prod-key vs dev-key) и в **feature-flag**
   `PLEXOR_MCP_DEV_MODE`. Prod-ключ никогда не видит dev-tools, даже
   если флаг включён — defense in depth.

2. **MCP не обходит RBAC.** Каждый tool call внутри хоста проходит
   через тот же `IAuthorizationService` + `TenantContext`, что и
   REST-запрос. Если user через UI не может удалить VM — агент тоже
   не может. Если может — тоже может (но это audited).

3. **Read-only first.** В v1 экспонируем **только** операции чтения.
   Причина: мутирующие операции требуют:
   - подтверждения (`create_vm` без диалога = опасность),
   - строгого audit (агент может сделать 1000 delete за минуту),
   - rich error responses (validation, conflict, partial-failure),
   - approval flows для destructive ops.

   Read-only tools могут стартовать сразу как только у модуля есть
   хоть какой-то query-service — никаких новых барьеров не нужно.

4. **Tool surface = проекция REST API, не отдельный API.** Каждый tool
   **тонкая обёртка** над существующим `IXxxQueryService` / `IXxxReadService`.
   Никакой бизнес-логики в MCP-handler. Иначе — расхождение логики
   между REST и MCP через полгода.

5. **Provider-aware.** Если ресурс реализован через provider-plugin
   (см. [providers.md](../providers.md)), tool работает через тот же
   `IResourceProvider` — без хардкода конкретного бэкенда (KVM/Ceph/etc).
   Для self-hosted user'а это значит: "если у меня MinIO вместо Ceph —
   `list_buckets` всё равно работает".

6. **Транспорт выбирается конфигом, не кодом.** `Mcp:Transport=stdio`
   для dev-сценариев (агент и Plexor на одной машине). `Http` для
   удалённых агентов (агент на ноуте, Plexor на сервере). Оба
   одновременно не запускаем — лишний footprint.

7. **MCP сервер = hosted service, не отдельный бинарь.** Стартует /
   останавливается вместе с Plexor.Host, шарт один DI-container,
   один DbContext pool. Никакого sidecar overhead.

8. **Tool versioning через MCP protocol version + Plexor version.** Tool
   имена стабильны в рамках мажорной версии Plexor. Breaking changes
   tool-имён или аргументов → новый tool с другим именем (deprecate
   старый, не удалять сразу — см. semantic versioning).

---

## Tool & Resource surface (v1+v2)

> **Decision (2026-07-09):** read-only операции экспонируются как **MCP
> resources** с URI-схемой `plexor://<category>/<id>` (или
> `plexor://<category>?<query>` для list). Мутирующие операции (v2) —
> **tools**. Разделение идиоматично по [MCP spec](https://modelcontextprotocol.io/docs/concepts/resources)
> и поддерживается всеми современными клиентами (Claude Code, Cursor).

Конкретный набор зависит от того, какие модули ship'нулись. **Принцип**:
по одному resource/tool на каждую операцию из REST API, плюс несколько
композитных.

### Resources (read-only, v1)

| Resource template | Источник (query-service) |
|-------------------|--------------------------|
| `plexor://vms/{vm_id}` | `Compute.IComputeQueryService.GetAsync` |
| `plexor://vms?{filter}` | `Compute.IComputeQueryService.ListAsync` |
| `plexor://vpcs/{vpc_id}`, `plexor://vpcs?{filter}` | `Network.INetworkQueryService` |
| `plexor://subnets/{id}`, `plexor://floating-ips/{id}`, `plexor://security-groups/{id}` | `Network.INetworkQueryService` |
| `plexor://volumes/{id}`, `plexor://buckets/{id}` | `Storage.IStorageQueryService` |
| `plexor://users/{user_id}` (resolve by API key) | `Identity.IIdentityQueryService` |
| `plexor://audit-log?{filter}` | `Identity.IAuditQueryService` |
| `plexor://clusters/{id}/health`, `plexor://nodes/{id}` | `Compute.IClusterQueryService` |
| `plexor://billing/summary?{period}` | `Billing.IBillingQueryService` |
| `plexor://tasks/{task_id}` | `Compute/Network.ITaskQueryService` (async op status) |

**Async tasks resource:** все мутирующие tools (v2) возвращают
`{task_id, status: "pending"}` сразу. Агент poll'ит
`plexor://tasks/{task_id}` чтобы получить `{status, progress, result, error}`.
Не блокирует tool call, идиоматично по MCP (read-only state = resource),
легко тестируется.

**Композитные resources** (полезные для агентов):

- `plexor://find?name={name}` — поиск ресурса любого типа по имени
  (агент говорит "та ВМ которую я вчера создавал" — мы находим по тегам +
  времени + fuzzy match по имени).
- `plexor://recent-changes?since={minutes}` — "что изменилось за последние N
  минут" = подмножество audit-log с фильтром.

### Tools (mutations, v2)

Deferred до тех пор пока модули не получат нормальные RBAC + audit:

- `create_vm`, `start_vm`, `stop_vm`, `reboot_vm`, `delete_vm`
- `create_volume`, `attach_volume`, `detach_volume`
- `create_bucket`, `put_object`, `delete_object`
- `create_user`, `grant_role`, `revoke_role`
- Все batched-ops ("создай 10 VM по списку")

---

## Dev-mode tools (только при `PLEXOR_MCP_DEV_MODE=true`)

| Tool | Что делает | Безопасность |
|------|-----------|--------------|
| `query_db(sql)` | SELECT-only к локальной dev-БД | Regex-guardrail `^\s*SELECT`; max rows; timeout 5s |
| `seed_fixtures(scenario)` | Грузит `*.json` fixture-файлы в dev-БД | Список разрешённых scenarios из конфига |
| `run_migration_target(version)` | Применяет миграцию до указанной версии | Dry-run first; только в dev-БД |
| `tail_node_log(node_id, lines)` | Читает последние N строк из лога Plexor.NodeAgent | Чтение через SSH / локальный файл — зависит от setup |
| `reset_tenant(tenant_id)` | **ТОЛЬКО в dev** — drop + recreate схемы тенанта | Требует отдельный dev-flag `PLEXOR_ALLOW_DESTRUCTIVE_DEV_OPS=true` |

Dev-tools **не линкуются** в prod-сборке:

```xml
<!-- src/host/Plexor.Host/Plexor.Host.csproj -->
<ItemGroup Condition="'$(Configuration)' != 'Production'">
  <ProjectReference Include="..\..\..\tools\Plexor.Mcp.Dev\Plexor.Mcp.Dev.csproj" />
</ItemGroup>
```

Plus runtime-проверка `PLEXOR_MCP_DEV_MODE` перед регистрацией tools
— даже если пакет случайно попал в prod-сборку, dev-tools не
зарегистрируются без env var.

---

## Auth model

**Tenant API key** (тот же, что для REST API) — bearer token в MCP
handshake (для stdio — env var `MCP_AUTH_TOKEN` при старте процесса;
для HTTP — header `Authorization: Bearer <key>`).

API key resolves в `TenantContext { TenantId, UserId, Roles[] }` один
раз на старте сессии (stdio) или на каждое соединение (HTTP). Tool
calls получают `TenantContext` через DI scope — как REST endpoints.

**Multi-tenancy:** один MCP-сервер обслуживает все тенанты. Tenant
определяется по API key. Агент видит **только** ресурсы своего тенанта.
Cross-tenant tool calls невозможны (нет такого tool).

**Revocation:** если API key revoked в Identity — следующий tool call
получает `401 Unauthorized`. Не нужно ничего делать на стороне MCP.

**В prod — TLS обязателен** для HTTP transport. Документируем в
`operations/install.md`: "MCP HTTP port без TLS = утечка tenant data
через любой man-in-the-middle".

---

## Safety posture

> **Decisions (2026-07-09):** мутирующие tools выполняются **без
> approval flow** (как YC-MCP / HashiCorp-MCP). Защита держится на трёх
> слоях — RBAC, rate-limit, audit. Наблюдаемость — в существующий
> `Plexor.Modules.Audit` с тегами `source=mcp` и `actor=api_key_id`.

### Три слоя защиты

1. **RBAC (тот же что в REST).** Каждый tool/resource call идёт через
   `IAuthorizationService` + `TenantContext`. Агент не может сделать
   то, что user не может через UI. Не вводим отдельный RBAC для MCP.

2. **Rate limit per API key.** Per-key sliding-window counter:
   - **Resources (read):** 600 calls/minute — щедро, ловит только
     runaway-агенты в infinite loop.
   - **Tools (mutations, v2):** 60 calls/minute — достаточно для
     "создай 10 VM по списку" за один заход, недостаточно для
     случайного масс-удаления.
   - **Dev-mode tools:** 600 calls/minute, но см. guardrails в
     секции Dev-mode.
   - Counter хранится в-memory (`ConcurrentDictionary<ApiKeyId, RateBucket>`)
     для MVP; при multi-instance — отдельная таблица в Audit/Identity
     модуле или Redis (дефер).

3. **Audit в `Plexor.Modules.Audit` с тегами.** Каждое событие MCP
   пишется как `AuditEvent { Source: "mcp", ActorType: "api_key",
   ActorId: <key_id>, TenantId: <tenant>, Action: <tool_name>,
   Args: <sanitized>, Result: "ok"|"failed", DurationMs: N, Timestamp }`.
   Self-hosted user фильтрует по `Source="mcp"` в существующем UI/CLI.

**`Args` санитизируется по deny-list** перед записью — реализация
в `AuditEventSanitizer` модуля Audit (не в MCP-коде, чтобы переиспользовать
для REST audit events тоже). Категории:

- **Secrets** (всегда redact): case-insensitive regex на имя поля —
  `password|secret|token|api_key|private_key|ssh_key_data`. Значение
  заменяется на `"[REDACTED]"`.
- **PII** (по умолчанию hash): `email|phone|ip_address|user_name`. По
  умолчанию значение хешируется (`sha256 + первые-8-символов`) — для
  корелируемости в audit queries. Per-tenant override через policy
  (`AuditPolicy.LogPiiAsIs=true` для тенанта, которому нужна видимость
  для debugging / customer-support).
- **Bulk content** (size threshold): поля `content|data|body|payload`
  если > 4KB заменяются на `{truncated: true, original_size: N}`.
  Ниже порога — пишутся as-is (полезно для debugging коротких payloads).
- **Internal paths**: пути вида `/var/plexor/tenants/{tid}/...`
  заменяются на `/var/plexor/tenants/{tid_redacted}/...`. External URLs
  (cdn.example.com) не трогаем.

### Что НЕ входит в safety posture (осознанно)

- ❌ Approval/elicitation перед мутациями. Агент может удалить VM
  без "are you sure?". См. Non-goals и Open Question 5.
- ❌ Отдельный MCP-audit-log (JSONL, файлы). Всё через единый
  Audit-модуль — single source of truth.
- ❌ Approval flow для `delete_*` отдельно от других мутаций. Если
  понадобится — расширим rate-limit (ниже лимит для delete) или
  добавим флаг `Tools:RequireApprovalForDestructive=true` в v3.

---

## Конфигурация

```yaml
# appsettings.yaml (или env: HYBRID_MCP__*)
Mcp:
  Enabled: false                      # off by default
  Transport: "http"                   # "stdio" | "http"
  Http:
    Port: 9443                        # отдельный от REST (8443)
    RequireTls: true                  # prod = true, local dev = false
    CorsOrigins: []                   # не нужны — агенты не из браузера
  DevMode:
    Enabled: false                    # PLEXOR_MCP_DEV_MODE
    AllowDestructive: false           # PLEXOR_ALLOW_DESTRUCTIVE_DEV_OPS
    Guardrails:
      QueryMaxRows: 1000
      QueryTimeoutMs: 5000
      AllowedFixtures: ["basic-tenant", "multi-tenant", "load-test"]
```

**Не отдельный раздел Identity.ApiKey** — key берётся из существующего
`Auth:ApiKey` секции, MCP не вводит новый auth-механизм.

---

## Дистрибуция

Два варианта, обсуждение deferred:

| Вариант | Плюсы | Минусы |
|---------|-------|--------|
| **A. Подпапка `src/host/Plexor.Host/Mcp/`** | Нуль overhead, один PR на всё, версионируется с Plexor.Host | Тяжело переиспользовать вне Plexor.Host; если кто-то хочет свой MCP поверх своих query-services — копипаста |
| **B. Отдельный NuGet `Plexor.Mcp`** | Чистая зависимость; возможно тестировать отдельно; provider-author может добавлять свои tools | Over-engineering на старте; +1 проект в solution; versioning complexity |

**Default пока — A** (подпапка в Host). Если в будущем появится
use-case для external consumption — выделим в NuGet. **YAGNI.**

---

## Open questions

Дефер до того как модули осядут и появится реальный нагрузочный тест.

1. **Tool count ceiling (v2).** С переходом read-only → resources
   список tools сокращается до мутаций (≤30 на ближайший год). Если
   мутаций станет 100+ — namespace-prefix (`compute.create_vm`)
   или resource-templates для команд. **Решим в v2.**

2. **Rate limit calibration.** 600/60 calls/min — стартовая оценка.
   Реальные числа после запуска self-hosted beta: сколько calls/min
   делает один Claude Code session, сколько делает orchestrator с
   10 агентами, false-positives от runaway-loops. **Калибруем по метрикам.**

3. **Elicitation в v3+.** MCP `elicitation` (agent спрашивает user
   перед dangerous op) может стать опциональной надстройкой когда
   клиенты (Claude Code / Cursor) будут стабильно её поддерживать.
   Сейчас поддержка фрагментарная (2025+ spec, частичная в Cursor).

---

## Non-goals

Что MCP-сервер **не делает** (явно):

- ❌ **Не чат-UI в Plexor Portal.** Это отдельная продуктовая фича
  (отдельный design-anchor, отдельный phase). MCP = protocol,
  UI = клиент LLM-провайдера (Claude/Cursor/etc), не наш.
- ❌ **Не LLM-прокси.** Plexor не знает какой LLM использует агент.
  Это транспорт для tools, не модельный слой.
- ❌ **Не обход RBAC / multi-tenancy.** (Принцип 2.)
- ❌ **Не альтернатива Terraform / Pulumi.** MCP — для ad-hoc через
  разговор. IaC остаётся IaC.
- ❌ **Не self-service onboarding.** Self-hosted user должен
  сгенерировать API key через существующий UI / `plx` CLI. MCP
  не предлагает "просто подключись без ключа".

---

## Когда это не Phase X

Plexor на текущем этапе — фронтенд на моках. Бэкенд (Plexor.Host)
только стартует. MCP не имеет смысла пока нет:

1. Compute query-service (для `list_vms` / `get_vm`)
2. Identity.ApiKey работающего end-to-end (для auth)
3. Хотя бы одного модуля с реальной бизнес-логикой (для demo tools)

**Estimated readiness:** после первых 2-3 модулей из
[modules.md](../modules.md) осядут. Не Phase 1, не сейчас.

До этого — дизайн-док, обсуждение, может быть spike (proof-of-concept
MCP-сервер на mock-данных для проверки SDK). Код прода — нет.

---

## Связанные документы

- [architecture.md](../architecture.md) — общая картина слоёв
- [modules.md](../modules.md) — какие модули дают query-services
- [providers.md](../providers.md) — provider-plugin pattern
- [api-contracts.md](../api-contracts.md) — OpenAPI workflow (MCP tools
  живут рядом, не вместо)
- [operations/install.md](../operations/install.md) — `plx init` флаги
  для enable MCP
- [.agents/rules/coding/cross-service-communication.md](../../rules/coding/cross-service-communication.md) —
  паттерн peer-сервиса (похожая модель: contract + DI + diagnostics)