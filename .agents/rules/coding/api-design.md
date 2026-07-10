---
description: asp.net core api design — controllers, [ProducesResponseType], AddProblemDetails, URL structure, endpoint attributes
globs: ["**/*.cs"]
always: true
---

# API design (ASP.NET Core)

Этот файл — правила ASP.NET Core: controllers, OpenAPI attrs, URL structure,
endpoint signatures. EF/logging — в `ef-core.md` и `logging.md`.

## 1. Controller skeleton

```csharp
[ApiController]
[Route($"{ApiRouteConstants.DefaultRoute}/record/futures")]
public sealed class FuturesRecordController(IFuturesQueryService futuresQueryService)
    : ControllerBase
{
    [HttpGet("instrument/page")]
    [EndpointName("futures-instrument-page")]
    [EndpointSummary("Returns paginated futures instruments")]
    [EndpointDescription("Supports filtering and sorting through the query parameters")]
    [Tags(["futures", "instruments"])]
    [ProducesResponseType<GridifyResult<FuturesInstrument>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GridifyResult<FuturesInstrument>>> GetInstrumentPageAsync(
        [FromQuery] GridifyQuery gridifyQuery,
        CancellationToken cancellationToken = default)
    {
        return Ok(await futuresQueryService.QueryAsync(gridifyQuery, cancellationToken));
    }
}
```

---

## 2. OpenAPI attributes

| Metadata | Attribute | Example |
|----------|-----------|---------|
| operationId | `[EndpointName]` | `[EndpointName("futures-instrument-page")]` |
| summary | `[EndpointSummary]` | `[EndpointSummary("Returns paginated...")]` |
| description | `[EndpointDescription]` | `[EndpointDescription("Supports...")]` |
| tags | `[Tags]` | `[Tags(["futures"])]` |
| response type | `[ProducesResponseType<T>]` | См. ниже |

---

## 3. `[ProducesResponseType<T>]` — 2xx mandatory per-endpoint, 4xx/5xx global

- **2xx success** (200, 201, 202, 204): `[ProducesResponseType<T>]` **обязателен** per-endpoint.
- **4xx/5xx errors** (400, 404, 409, 422, 500, 503): **глобально** через
  `builder.Services.AddProblemDetails()` в `Program.cs` — **не нужно**
  ставить атрибут на каждый action.
- **Override per-endpoint** — только когда error имеет специфическую форму
  (например, `409 Conflict` с machine-readable `code`).

```csharp
// ✅ Correct — 200 обязателен, 4xx/5xx глобально
[HttpGet("{userId:guid}")]
[EndpointName("users-get-by-id")]
[ProducesResponseType<UserModel>(StatusCodes.Status200OK)]
public async Task<ActionResult<UserModel>> GetUserAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
{
    return await userService.GetAsync(userId, cancellationToken) is { } user
        ? Ok(user)
        : NotFound();   // → ProblemDetails автоматически через AddProblemDetails()
}

// ❌ Wrong — ProblemDetails на каждом action, copy-paste noise
[HttpGet("{userId:guid}")]
[ProducesResponseType<UserModel>(StatusCodes.Status200OK)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public async Task<ActionResult<UserModel>> GetUserAsync(Guid userId, ...)
```

**Запрещён:** `[SwaggerOperation]` из `Swashbuckle.AspNetCore.Annotations`.

---

## 4. Endpoint signatures

- Возврат — `ActionResult<T>` (или `ActionResult` для void-ответов).
- `CancellationToken` последним: `CancellationToken cancellationToken = default`.
- `await` всегда (`.Result` запрещён).
- Если эндпоинт — однострочный проброс — он остаётся `async` + `Ok(await ...)`,
  потому что иначе сигнатура `Task<ActionResult<T>>` не сходится.

---

## 5. URL structure & `ApiRoutes` constants

Все URL — `api/{version}/{resource}/...`. Версия — **одна константа** в общем
классе, чтобы при выпуске `v2` поменять в одном месте.

```csharp
namespace Acme.Shop.Api.Public;

/// <summary>Single source of truth for API URL composition.</summary>
public static class ApiRoutes
{
    public const string ApiVersion = "v1";
    public const string Base = $"api/{ApiVersion}";   // "api/v1"
}

[ApiController]
[Route($"{ApiRoutes.Base}/[controller]")]   // /api/v1/{controller}
public sealed class TasksController(ITaskService taskService) : ControllerBase
{
    [HttpGet]                                  // GET /api/v1/tasks
    [EndpointName("tasks-list")]
    public Task<ActionResult<IReadOnlyList<TaskResponse>>> ListAsync(...) { ... }
}
```

### Route constraints — обязательны для типизированных ID

| Сегмент | Что значит |
|---------|-----------|
| `{taskId:guid}` | Только Guid |
| `{slug:length(2,50)}` | 2–50 символов |
| `{slug:regex(^[a-z0-9-]+$)}` | Kebab-case |
| `{page:int:min(1)}` | int ≥ 1 |

### Resource naming — plural

| Singular (неправильно) | Plural (правильно) |
|------------------------|---------------------|
| `/api/v1/task` | `/api/v1/tasks` |
| `/api/v1/executionPlan` | `/api/v1/execution-plans` (kebab-case) |

Multi-word ресурсы — **kebab-case** в URL.

**Исключение:** action-методы (`/cancel`, `/retry`), health probes
(`/health`), `by-...` поиски (`/by-slug/...`).

---

## 6. Controller structure — hybrid (resource + lifecycle)

**Resource controller** — `{Resource}Controller`, стандартный CRUD.

**Lifecycle controller** — `{Domain}{Action}Controller` или
`{Actor}{Action}Controller`, не CRUD: state machine (claim → heartbeat →
release), другой клиент (worker SDK vs dashboard), cross-resource, не-RESTful.

| Контроллер | Маршруты | Тип |
|------------|----------|-----|
| `RunsController` | `GET /api/v1/runs`, `POST /api/v1/runs`, `GET /api/v1/runs/{id}` | Resource CRUD |
| `WorkersController` | `GET /api/v1/workers`, `GET /api/v1/workers/{id}` | Resource CRUD |
| `WorkerClaimController` | `POST /api/v1/workers/claim`, `POST /api/v1/workers/{id}/heartbeat` | Lifecycle |

**Сигнал "выноси в отдельный контроллер"** (любой из):
- Другой клиент (worker vs dashboard).
- State machine — не CRUD.
- Cross-resource.
- Не-RESTful семантика.

`WorkersController` (CRUD) и `WorkerClaimController` (lifecycle) сосуществуют.

---

## 7. Endpoint attributes

**Обязательные (per-endpoint):**
- `[HttpGet]` / `[HttpPost]` / `[HttpPut]` / `[HttpDelete]` / `[HttpPatch]`
  с явным шаблоном, если путь не очевиден из convention.
- `[ProducesResponseType<T2xx>]` для success.

**Рекомендуемые (per-endpoint):**
- `[EndpointName("kebab-case-name")]` — operationId для OpenAPI.
- `[EndpointSummary("...")]` — короткое summary.
- `[Tags(["domain", "subdomain"])]` — группировка.

**Глобально (в `Program.cs`):**
- `builder.Services.AddProblemDetails();` — все 4xx/5xx автоматически.

**Override per-endpoint** — только при необходимости.

---

## 8. Endpoint-specific dependencies — `[FromServices]`

Сервис, который нужен **только одному endpoint**, не в конструкторе. Через
`[FromServices]` на параметре action-метода. См. `anti-patterns.md` §5.

---

## 9. Records DTO — отдельный файл

Request/Response records живут в `Application/Models/`, отдельный файл на
тип. Никогда inline в controller. См. `anti-patterns.md` §2.

---

## 10. Anti-patterns

```csharp
// ❌ Magic strings в URL
[Route("api/v1/tasks")]
public sealed class TasksController : ControllerBase

// ❌ Конкатенация строк в контроллере (нужен CreatedAtAction)
return Created($"/api/v1/tasks/{task.Id}", task);

// ❌ [controller] не работает с другим префиксом
[HttpGet("v1/special")]   // → /api/v1/tasks/v1/special, баг

// ❌ 4xx/5xx на каждом action — copy-paste noise
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
[ProducesResponseType<ProblemDetails>(500)]

// ❌ ProblemDetails в success response type
[ProducesResponseType<ProblemDetails>(StatusCodes.Status200OK)]

// ❌ void/ничего-не-возвращает action — ASP.NET Core всё равно ждёт ActionResult
public async Task DeleteAsync(Guid id) { ... }
```

---

## 11. API versioning — URL-prefix для v0.1

Версия — сегмент URL. Single source of truth =
`Plexor.Shared.Contracts.Routes.ApiRoutes`. Каждый контроллер
композит `[Route]` через эту константу:

```csharp
[ApiController]
[Route($"{ApiRoutes.Base}/nodes")]   // → /api/v1/nodes
public sealed class NodeController : ControllerBase
{ ... }
```

`Base` определён один раз:
```csharp
public const string ApiVersion = "v1";
public const string Base = "api/" + ApiVersion;   // const expression
```

Bump версии = поменять одну константу в `ApiRoutes.cs`. Каждый
контроллер в solution автоматически получает новый prefix.

### Почему URL-prefix (а не header / attribute-based)

- Одна константа = один bump. Новый controller для v2 = новый
  class на `${ApiRoutes.Base}/v2/...` (или новый `Base` для v2).
  Не нужно забывать `[ApiVersion("2.0")]` attribute на каждом
  классе.
- v0.1 имеет ровно одно поколение. Sub-versioning
  ([ApiVersion] attribute) — это v0.2+ concern, когда
  сосуществующие поколения станут реальной вещью.

### Antipatterns

- ❌ **Hardcoded `"api/v1"` в `[Route]`** — обходит константу,
  ломает v2 bump. Используй `${ApiRoutes.Base}/...`.
- ❌ **`ApiRoutes.Resource(name)` в `[Route]`** — `Resource` это
  method, не const. Attribute arguments требуют const
  expressions. Композь через `$"..."`, ссылаясь на
  `ApiRoutes.Base` (a const).
- ❌ **Два контроллера на одном route** — ambiguous routing. v2
  controller идёт на другой `Base`, не на тот же route под
  другим attribute.

### Self-audit grep

```bash
# Hardcoded "api/v" в [Route] — должно быть пусто
rg -n '\[Route\("api/v' src/ --type cs

# Resource method в attribute (не const) — должно быть пусто
rg -n '\[Route\(ApiRoutes\.Resource' src/ --type cs
```

### Когда переключаться на attribute-based sub-versioning

- Два поколения API должны сосуществовать в одном deployment
  (редко — наш deployment model = один host per generation).
- Frontend должен вызывать v1 и v2 endpoints в одной сессии
  (deprecation period).
- Sub-granularity версии (v1.1, v1.2) внутри major — используй
  `[ApiVersion("1.1")]` параллельно с URL v1.

---

## Связанные правила

- `ef-core.md` — entity framework
- `logging.md` — structured logging
- `di-installer.md` — DI registration
- `anti-patterns.md` — records DTO, FromServices, validation