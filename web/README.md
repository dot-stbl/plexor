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

## Architecture decisions

See `.agents/docs/ui/architecture.md` for the full stack rationale.

## Codegen

```bash
bun run codegen    # regenerate shared/api from openapi.json
```

Generated files are gitignored — never edit by hand.
