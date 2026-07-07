# Plexor UI — Architecture decisions

> Frontend stack and layout for Plexor Portal. Decisions frozen 2026-07-07.
> Source-of-truth for: новые компоненты, codegen workflow, тестовый стэк.

## TL;DR

```
┌──────────────────────────────────────────────────────────────────────┐
│  Browser                                                            │
└──────────────────────────────────────────────────────────────────────┘
        │ HTTPS (PKCE, JWT в Authorization header)
        ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Plexor.Host (ASP.NET Core 10) — control plane                       │
│  ┌────────────┐  ┌──────────────────┐  ┌────────────────────────┐    │
│  │  /api/*    │  │  /scalar,        │  │  /* (everything else)   │    │
│  │  REST API  │  │  /openapi.json   │  │  ↓ SPA fallback         │    │
│  │  endpoints │  │  (API docs)      │  │  UseSpa / StaticFiles   │    │
│  └────────────┘  └──────────────────┘  └────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────┘
                       │ dev: proxy
                       ▼
        ┌──────────────────────────────┐
        │  Vite dev server (:5173)     │
        │  - HMR over websockets        │
        │  - Sourcemaps                 │
        └──────────────────────────────┘
```

## Stack — зафиксированные решения

| # | Слой | Решение | Почему |
|---|------|---------|--------|
| 1 | Package manager | **bun** | быстрый install, единый toolchain |
| 2 | Bundler | **Vite** | зрелый, UseProxyToSpaDevelopmentServer работает из коробки |
| 3 | Dev integration | **UseSpa + UseProxyToSpaDevelopmentServer** | один origin, нет CORS, backend проксирует на Vite в dev |
| 4 | Design system | **shadcn/ui + Plexor tokens** (на Base UI) | компоненты в нашем репо (agent-readable), Base UI primitives (преемник Radix) |
| 5 | Styling | **Tailwind CSS** | de facto для shadcn/Radix |
| 6 | Routing | **TanStack Router** | file-based, type-safe params + search validation |
| 7 | Server state | **TanStack Query** | cache, invalidation, devtools, генерится kubb |
| 8 | Forms | **React Hook Form + Zod** | type-safe валидация, Zod-схемы генерятся kubb |
| 9 | API client gen | **kubb** | types + Zod + TanStack Query hooks + MSW + Faker |
| 10 | Auth | **Plexor local** (MVP), **Keycloak** позже как `IAuthProvider` | свой auth для MVP, Keycloak не блокирует |
| 11 | Token storage | **Memory + silent refresh** (refresh в httpOnly cookie) | XSS-safe |
| 12 | Test runner | **Vitest** | Vite-native, snapshot/mock API |
| 13 | API mocking | **MSW** (через kubb-plugin-msw + kubb-plugin-faker) | double-consistency с продом |
| 14 | E2E | **Playwright** | cross-browser, codegen, parallel |

## Layout

```
web/
├── package.json                    # bun workspaces root
├── bun.lockb
├── bunfig.toml
├── apps/
│   └── console/                    # Plexor Portal — единственное приложение в MVP
│       ├── index.html
│       ├── vite.config.ts
│       ├── src/
│       │   ├── main.tsx           # bootstrap: QueryClient + Router + Provider
│       │   ├── routes/             # TanStack Router file-based
│       │   │   ├── __root.tsx
│       │   │   ├── index.tsx
│       │   │   ├── tenants/
│       │   │   ├── compute/
│       │   │   └── billing/
│       │   └── features/           # business features, сгруппированные по модулям
│       └── tsconfig.json
├── shared/
│   ├── ui/                         # @plexor/ui — Plexor DS (shadcn-style)
│   │   ├── src/
│   │   │   ├── primitives/        # Button, Input, Table, Form, Dialog
│   │   │   ├── tokens.css         # CSS variables: color, spacing, radius
│   │   │   └── index.ts
│   │   └── package.json
│   ├── lib/                        # @plexor/lib — hooks, utils
│   └── api/                        # @plexor/api — generated (kubb), gitignored
├── tooling/
│   ├── codegen/                    # kubb config + custom plugins (Filter DSL plugin позже)
│   │   ├── kubb.config.ts
│   │   └── package.json
│   ├── eslint-config/              # shared ESLint rules
│   └── tsconfig/                   # base.json, app.json, lib.json
└── apps/console/.env               # dotenv: API_BASE_URL, etc.
```

## Codegen workflow

```
[hybrid]/contracts/openapi.yaml   ← источник правды
        │
        │  Plexor.Host `dotnet build`
        ▼
artifacts/openapi.json             ← сгенерировано из ASP.NET OpenAPI source-gen
        │
        │  bun run codegen
        ▼
tooling/codegen/kubb.config.ts     ← читает openapi.json, применяет плагины
        │
        ├──> shared/api/types/             (@kubb/plugin-oas)
        ├──> shared/api/client/            (@kubb/plugin-client)
        ├──> shared/api/hooks/             (@kubb/plugin-react-query)
        ├──> shared/api/schemas/           (@kubb/plugin-zod)
        ├──> shared/api/msw/               (@kubb/plugin-msw)
        └──> shared/api/fixtures/          (@kubb/plugin-faker)
```

**Правило:** `shared/api/**` — gitignored, перегенерируется при каждом `bun run codegen`. Агенты читают сгенерированный код как reference, не редактируют.

## Backend integration — Plexor.Host

```csharp
// Program.cs (sketch)
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSpaFallback(builder.Configuration);  // Plexor.Shared.Composition.Spa
// ... модули, OpenAPI, etc.

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(...);
}

app.MapControllers();              // /api/* endpoints

app.UseSpaFallback();              // /api, /scalar, /openapi, /health — bypass SPA
                                  // everything else → static files (prod) / Vite proxy (dev)

app.Run();
```

Конфигурация:
```json
{
  "Spa": {
    "BackendPaths": ["/api", "/scalar", "/openapi", "/health"],
    "SourcePath": "../../web/apps/console",
    "DevServerUrl": "http://localhost:5173"
  }
}
```

## Auth flow — Plexor local (MVP)

```
1. UI → GET /.well-known/openid-configuration-like (Plexor's own discovery)
2. UI → POST /api/v1/auth/login (email/password) → access_token + refresh_token
3. UI: access_token → memory (React state, Context), refresh_token → httpOnly cookie (Set-Cookie)
4. UI → every API call: Authorization: Bearer <access_token>
5. Plexor.Host JWT middleware validates signature + exp
6. When access_token expires (< 5 min):
   a. UI fires silent refresh: POST /api/v1/auth/refresh (cookie auto-sent)
   b. Backend validates refresh_token from cookie, issues new access_token
   c. UI updates memory token
```

**Будущее:** Keycloak подключается через `IAuthProvider` interface, тот же flow но через Keycloak's OIDC endpoints. Агенты не меняются.

## Testing strategy

| Уровень | Tool | Что покрывает |
|---|---|---|
| Unit | Vitest | чистые функции, hooks, utils |
| Component | Vitest + Testing Library + MSW | компоненты с реальным client code + intercepted fetch |
| Integration | Vitest + MSW (kubb-generated handlers) | client flows: list → detail → mutation → invalidation |
| E2E | Playwright | full browser, real backend (Testcontainers) |

**Testability контракт:** для каждого фичевого модуля агент пишет:
- `*.unit.test.ts` — для чистой логики
- `*.integration.test.tsx` — через kubb-generated hooks + MSW
- `*.e2e.test.ts` — через Playwright (опционально для MVP)

## Агентская разработка

**Что агенты могут делать сами** (predictable, на основе этой архитектуры):
- Добавлять новые routes в `apps/console/src/routes/`
- Создавать новые компоненты в `shared/ui/src/primitives/` (по shadcn паттерну)
- Использовать kubb-generated hooks (`shared/api/hooks/`)
- Писать Vitest тесты с MSW handlers из `shared/api/msw/`

**Что требует ручного решения** (ask user):
- Новые external libraries в `package.json`
- Изменения в `vite.config.ts`, `kubb.config.ts`
- Изменения в CSP / auth headers
- Новые плагины в codegen pipeline

## Roadmap — что НЕ в MVP

| Фича | Когда | Почему |
|---|---|---|
| i18n | Phase 2 | MVP англоязычный |
| Light/dark mode toggle | Phase 2 | Plexor DS по умолчанию light |
| Sentry/OTel на FE | Phase 2 | достаточно логов в MVP |
| Admin app (`apps/admin/`) | Phase 2 | один app в MVP |
| Provider SDK UI | Phase 2 | когда появится marketplace |

## Open questions (для следующих обсуждений)

1. **Project init script / CLI** — что делает, как пишется (ты пушишь)
2. **Filter DSL kubb plugin** — после init script (kubb плагин пишется как TS пакет)
3. **CSP / security headers** — `frame-ancestors`, `script-src` для Keycloak iframe
4. **i18n choice** — react-i18next vs lingui vs FormatJS когда дойдёт

## Связанные документы

- `../architecture.md` — общая архитектура платформы
- `../modules.md` — модули (то что UI отображает)
- `../providers.md` — provider SDK (UI отображает каталог)
- `../api-contracts.md` — OpenAPI workflow
- `brand.md` — Plexor Brand
- `personas.md` — 4 personas (Dmitriy, Maria, Andrey, Vasya)
- `screens/` — дизайн-экраны
- `user-flows.md` — критичные user flows