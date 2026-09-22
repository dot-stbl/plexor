# Plexor dev port pool

The Plexor offline monorepo owns the contiguous 100-port block
**`17100-17199`** for local development. This is a reservation, not a
free-for-all — every port in use must be in the table below.

## Why this range

The block is in the IANA dynamic/private range (49152-65535 is the
OS-assigned ephemeral range, so anything below it is reusable). 17100 is
above 1024 (no root required), below 65535 (well under the port limit),
and deliberately away from the JS ecosystem's usual grounds: not the
Next/CRA/Storybook 3000s, not Vite's default 5173, not Storybook's
default 6006. It also keeps visual separation from the backend's
control-plane API at 48001/48002 — FE apps at 17xxx, control plane at
48xxx, no accidental cross-binds.

## Assignments

| Port | Surface |
|---|---|
| 17100 | `@plexor/console` — Vite dev server |
| 17101 | `@plexor/docs` — Vite dev server |
| 17110 | `@plexor/console` — Vite preview (built `dist/`) |
| 17111 | `@plexor/docs` — Vite preview |
| 17120 | Storybook UI gallery |
| 17130 | Storybook test-runner HTTP |
| 17140 | Dev metrics scratch (Grafana / Prometheus / Tempo) |
| 17150-17159 | `@plexor/marketplace` (future app) |
| 17160-17169 | `@plexor/tenant-portal` (future app) |
| 17170-17179 | Plexor MCP server (future) |
| 17180 | `@plexor/nodeagent` debug HTTP |
| 17190-17199 | Reserved (`dev-bridge` shared infra, etc.) |

## Pattern — suffix groups by class

The third digit groups by surface class, the last two by instance:

| Suffix | Class |
|---|---|
| `x00-x09` | FE app — Vite dev server |
| `x10-x19` | FE app — Vite preview (built `dist/`) |
| `x20-x29` | Storybook UI gallery |
| `x30-x39` | Storybook test-runner HTTP |
| `x40-x49` | Observability scratch (Grafana / Prometheus / Tempo) |
| `x50-x59` | `@plexor/marketplace` |
| `x60-x69` | `@plexor/tenant-portal` |
| `x70-x79` | Plexor MCP server |
| `x80-x89` | `@plexor/nodeagent` debug HTTP |
| `x90-x99` | Shared infra (`dev-bridge`, etc.) |

Adding a new app: pick the next free slot in the appropriate class,
document it in the table above, and set `strictPort: true` in the new
Vite config so a clash is loud, not silent.

## Production is separate

These ports are **dev-only**. Production ports are intentionally separate
— the 17xxx block must NOT bleed into Dockerfile, nginx, or systemd
configs. Treat 17100+ as a workspace reservation that exists only on
the dev box.