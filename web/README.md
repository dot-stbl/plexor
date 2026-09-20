# Plexor Web — UI monorepo

Frontend for Plexor Portal. bun workspaces + Vite + React + TanStack.

## Layout

```
web/
├── apps/
│   └── console/          # Plexor Portal (Vite SPA)
├── shared/
│   ├── ui/               # Plexor DS (shadcn-style on Base UI)
│   ├── lib/              # hooks, utils
│   └── api/              # generated (kubb) — gitignored
└── tooling/
    └── codegen/          # kubb config + custom plugins
```

## Setup

```bash
cd web
bun install
bun run dev
```

## Gate

Before committing FE changes, from `web/apps/console`:

```bash
bun run gate    # typecheck + lint + test — must exit 0
```

CI (`.github/workflows/web.yml`) runs the same gate plus production builds
(`build`, `build:mock`, `build:storybook`) and a codegen drift-check.

## Architecture decisions

See `.agents/docs/ui/architecture.md` for the full stack rationale.

## Codegen

```bash
cd web/tooling/codegen
bun run generate    # regenerate apps/console/src/shared/api/src from contracts/plexor.openapi.yaml
```

The generated client is committed — regenerating on a clean checkout must
produce no diff (CI asserts this).
