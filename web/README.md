# Plexor Web — UI monorepo

Frontend for Plexor Portal. bun workspaces + Vite + React + TanStack.

## Layout

```
web/
├── apps/
│   ├── console/          # Plexor Portal (Vite SPA, dev port 17100)
│   └── docs/             # Plexor public docs site (Vite SPA, dev port 17101)
├── shared/
│   ├── ui/               # Plexor DS (shadcn-style on Base UI)
│   ├── lib/              # hooks, utils
│   └── api/              # generated (kubb) — gitignored
└── tooling/
    ├── codegen/          # kubb config + custom plugins
    └── stories/          # MDX prose / docs primitives (shared)
```

`web/apps/docs/` serves both the marketing landing (`/`) and the
operator-facing documentation (`/docs/*`). It shares `@plexor/ui` and
the Plexor DS tokens with `@plexor/console`. Full details, dev
workflow, MDX primitives, theming, and routing rules live in
`web/apps/docs/AGENTS.md`; page inventory in `web/docs/CONTENT-PLAN.md`;
dev port assignments in `web/docs/PORTS.md`.

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
