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

## Marketplace API

`Plexor.Modules.Marketplace` exposes endpoints для browse/install/manage
app providers и instances. Подробнее про provider format:
[providers.md](providers.md#2-app-providers-marketplace).

### Endpoints

#### `GET /api/v1/marketplace/providers`

List installed app providers (catalog).

```bash
curl -H "Authorization: Bearer $JWT" http://plexor.local/api/v1/marketplace/providers
```

```json
[
  {
    "name": "wordpress",
    "version": "0.2.0",
    "displayName": "WordPress",
    "description": "Popular open-source CMS",
    "category": "cms",
    "tier": "official",
    "icon": "https://catalog.plexor.dev/icons/wordpress.svg",
    "homepage": "https://wordpress.org",
    "installedAt": "2026-04-15T10:30:00Z"
  },
  {
    "name": "postgresql",
    "version": "15.3.0",
    "displayName": "PostgreSQL",
    "category": "database",
    "tier": "official"
  }
]
```

#### `GET /api/v1/marketplace/providers/{name}`

Get full provider details (config schema, resources, lifecycle hooks).

```bash
curl -H "Authorization: Bearer $JWT"   http://plexor.local/api/v1/marketplace/providers/wordpress
```

```json
{
  "name": "wordpress",
  "version": "0.2.0",
  "spec": {
    "resources": {
      "cpu": "500m",
      "memory": "512Mi",
      "disk": "10Gi",
      "ports": [{ "port": 80, "protocol": "TCP", "expose": true }]
    },
    "config": [
      { "name": "siteTitle", "type": "string", "required": true },
      { "name": "adminEmail", "type": "string", "required": true, "validation": "email" },
      { "name": "databaseSize", "type": "enum", "values": ["small","medium","large"], "default": "small" }
    ],
    "dependencies": {
      "services": [{ "name": "postgresql", "provider": "postgresql", "minVersion": ">=14.0", "create": true }]
    },
    "hooks": {
      "install": 3,
      "upgrade": 1,
      "uninstall": 2
    },
    "healthCheck": {
      "type": "http",
      "endpoint": "/",
      "port": 80,
      "expectedStatus": 200
    }
  }
}
```

#### `POST /api/v1/marketplace/providers`

Install a provider from source (git/OCI/tarball).

```bash
curl -X POST http://plexor.local/api/v1/marketplace/providers   -H "Authorization: Bearer $JWT"   -H "Content-Type: application/json"   -d '{"source":"github.com/community/plexor-provider-nginx","version":"1.0.0"}'
```

**source formats:**
- `github.com/owner/repo` — git clone
- `https://gitlab.com/...` — git clone
- `oci://registry/path:tag` — OCI artifact pull
- `./local-path` — local directory
- `./file.tar.gz` — local tarball

Response `202 Accepted`:
```json
{ "name": "nginx", "version": "1.0.0", "status": "installing" }
```

#### `DELETE /api/v1/marketplace/providers/{name}`

Uninstall provider. Fails if instances are still using it.

```bash
curl -X DELETE -H "Authorization: Bearer $JWT"   http://plexor.local/api/v1/marketplace/providers/nginx
```

#### `POST /api/v1/marketplace/instances`

Deploy an app instance.

```bash
curl -X POST http://plexor.local/api/v1/marketplace/instances   -H "Authorization: Bearer $JWT"   -H "Content-Type: application/json"   -d '{
    "provider": "wordpress",
    "version": "0.2.0",
    "config": {
      "siteTitle": "My Blog",
      "adminEmail": "jane@acme.com",
      "databaseSize": "small"
    }
  }'
```

Response `202 Accepted`:
```json
{ "id": "wp-7f3a2c", "status": "installing" }
```

Async install flow:
1. Validate config against provider schema (400 on bad input)
2. Resolve dependencies (auto-install postgresql if not present)
3. Allocate resources (volume, floating IP) via Compute/Network/Storage modules
4. Pick target node based on resources + tenant affinity
5. Write `provider_instances` row (status=installing)
6. Publish `plexor.app.install` to NATS
7. NodeAgent receives, runs install hooks, publishes status

#### `GET /api/v1/marketplace/instances`

List running instances (scoped by tenant from JWT).

```bash
curl -H "Authorization: Bearer $JWT"   http://plexor.local/api/v1/marketplace/instances
```

```json
[
  {
    "id": "wp-7f3a2c",
    "provider": "wordpress",
    "version": "0.2.0",
    "instanceName": "wp-7f3a2c",
    "status": "running",
    "config": { "siteTitle": "My Blog", "adminEmail": "jane@acme.com", "databaseSize": "small" },
    "resources": {
      "nodeId": "node-01",
      "ipAddress": "203.0.113.42",
      "ports": [{ "port": 80, "expose": true }],
      "volumeIds": ["vol-9a1b"]
    },
    "createdAt": "2026-04-15T10:30:00Z",
    "updatedAt": "2026-04-15T10:35:12Z"
  }
]
```

#### `GET /api/v1/marketplace/instances/{id}`

Get instance details.

```bash
curl -H "Authorization: Bearer $JWT"   http://plexor.local/api/v1/marketplace/instances/wp-7f3a2c
```

#### `DELETE /api/v1/marketplace/instances/{id}`

Uninstall instance (runs uninstall hooks).

```bash
curl -X DELETE -H "Authorization: Bearer $JWT"   http://plexor.local/api/v1/marketplace/instances/wp-7f3a2c
```

Async — returns 202. State transitions: running → uninstalling → removed.

#### `POST /api/v1/marketplace/instances/{id}/upgrade`

Upgrade to new provider version.

```bash
curl -X POST -H "Authorization: Bearer $JWT"   -H "Content-Type: application/json"   -d '{"toVersion": "0.3.0"}'   http://plexor.local/api/v1/marketplace/instances/wp-7f3a2c/upgrade
```

State transitions: running → upgrading → running (with new version) or failed.

#### `GET /api/v1/marketplace/instances/{id}/logs?tail=200`

Last N lines of provider install/upgrade logs (for debugging).

```bash
curl -H "Authorization: Bearer $JWT"   "http://plexor.local/api/v1/marketplace/instances/wp-7f3a2c/logs?tail=200"
```

```json
{
  "instanceId": "wp-7f3a2c",
  "logs": [
    "2026-04-15T10:30:01Z [install] pull-image: pulling wordpress:0.2.0...",
    "2026-04-15T10:30:15Z [install] create-config: writing wp-config.php",
    "2026-04-15T10:30:18Z [install] start-container: podman run -d --name=wp-7f3a2c...",
    "2026-04-15T10:30:45Z [install] wait-ready: HTTP 200 OK"
  ]
}
```

### OpenAPI generation

All Marketplace endpoints are exposed via Plexor.Modules.Marketplace's
endpoints installer (`IModuleEndpointsInstaller`) and appear in
`artifacts/openapi.json` after each build.

Frontend type-safe client is generated via Kubb (see top of file).
UI uses generated `getMarketplaceProviders()`, `createMarketplaceInstance()`, etc.
