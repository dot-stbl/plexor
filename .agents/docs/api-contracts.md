# API Contracts — OpenAPI workflow

Plexor использует **ASP.NET Core OpenAPI source-generator** для
автоматической генерации OpenAPI-спецификации + **Kubb** для генерации
typed API-клиента на фронте.

## Backend OpenAPI generation

`Plexor.Host` настроен на emit `artifacts/openapi.json` при каждом
`dotnet build`:

```xml
<PropertyGroup>
  <OpenApiDocumentsDirectory>../../../artifacts</OpenApiDocumentsDirectory>
  <OpenApiGenerateDocumentsOptions>--file-name openapi</OpenApiGenerateDocumentsOptions>
</PropertyGroup>
```

Это происходит в build-фазу `GenerateOpenApiDocuments` (вызывается из
`Microsoft.Extensions.ApiDescription.Server` после компиляции).

### Как endpoint'ы попадают в OpenAPI

```csharp
public sealed class VmEndpoints : IEndpointsInstaller
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/compute/vms")
            .WithTags("compute-vms")
            .RequireAuthorization();

        group.MapGet("/", async (VmsListRequest req, ISender sender) =>
            Results.Ok(await sender.Send(req)))
            .WithName("ListVms")
            .WithOpenApi();

        group.MapPost("/", async (
            [FromBody] CreateVmRequest req,
            [FromServices] ISender sender,
            CancellationToken ct) =>
        {
            var vm = await sender.Send(req, ct);
            return Results.Created($"/api/v1/compute/vms/{vm.Id}", vm);
        })
        .WithName("CreateVm")
        .WithOpenApi(op =>
        {
            op.Summary = "Create a virtual machine";
            op.Description = "...";
            return op;
        });
    }
}
```

### Conventions

- All endpoints under `/api/v1/{module}/{resource}`
- Return `Results.Ok()` / `Results.Created()` / `Results.Accepted()`
- Errors через `Results.Problem()` (ProblemDetails RFC 7807)
- Async, all `CancellationToken` honored
- Auth: every endpoint requires `RequireAuthorization()` except public ones

## Frontend codegen

```bash
cd web/apps/console
pnpm codegen
```

Это запускает Kubb generator, который:

1. Читает `../../../artifacts/openapi.json`
2. Генерирует TypeScript-файлы в `src/shared/api/generated/`:
   - `index.ts` — корневой re-export
   - `models/CreateVmRequest.ts` — типизированная модель
   - `api/createVm.ts` — функция с типом
   - `schemas.ts` — Zod-схемы для валидации

```typescript
// generated/api/createVm.ts (auto-generated)
export const createVm = async (params: {
  body: CreateVmRequest;
}): Promise<VirtualMachine> => {
  const response = await fetch('/api/v1/compute/vms', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(params.body),
  });
  // ... error handling, types
};
```

## API versioning

- URL prefix: `/api/v1/`, `/api/v2/` (по URL, не header)
- Deprecation: response header `X-Plexor-Deprecated: true` + `Sunset` date
- Compatibility window: 6 months для v1 → v2 transition

## Error format

```json
{
  "type": "https://plexor.dev/errors/quota-exceeded",
  "title": "Quota exceeded",
  "status": 403,
  "detail": "Project 'prod' has reached its maximum of 10 VMs",
  "instance": "/api/v1/compute/vms",
  "traceId": "00-...",
  "errors": [
    {
      "code": "compute.vms.quota",
      "field": "spec.count"
    }
  ]
}
```

## Rate limiting (per-tenant)

- Default: 1000 requests/hour per tenant
- Override per API: `compute.vms.create` = 10/hour
- Response header `X-RateLimit-Remaining`, `X-RateLimit-Reset`
- 429 Too Many Requests с `Retry-After`

## Pagination

Cursor-based (a-la Yandex Cloud):

```
GET /api/v1/compute/vms?pageSize=50&pageToken=eyJpZCI6IjEyMyJ9

Response:
{
  "items": [...],
  "nextPageToken": "eyJpZCI6IjE3NCJ9",  // null если последняя страница
  "totalSize": 247                       // опционально, expensive
}
```

## Filtering

Yandex-Cloud-style filters: `<field>.<op>.<value>`:

```
GET /api/v1/compute/vms?filter=status.phase=RUNNING AND spec.flavor=medium

Ops:
  =      equal
  !=     not equal
  >      greater
  <      less
  >=     greater-or-equal
  <=     less-or-equal
  :      substring (для строк)
  IN     in list: =IN[a,b,c]
```

## Idempotency

Все мутирующие операции поддерживают `Idempotency-Key` header:
- Если тот же ключ в течение 24h — возвращается cached result
- Защита от двойных кликов UI

## См. также

- [architecture.md](architecture.md) — где живёт OpenAPI emit
- [modules.md](modules.md) — API endpoints каждого модуля
- [ui.md](ui.md) — фронтовое использование generated client