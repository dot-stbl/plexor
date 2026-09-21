# plexor

**self-hosted cloud platform**

---

A modular monolith for self-hosted infrastructure.

plexor gives you cloud-like ergonomics on your own hardware —
control plane, compute, networking, identity, audit, all in
one .NET codebase.

## Status

`v0.x` — early development. The plans are public, the code is
public, the runtime is not stable yet. Don't deploy this in prod.

## Stack

| Layer | What |
|--|--|
| Backend | ASP.NET Core 10, EF Core 10, PostgreSQL |
| Frontend | React 19, TanStack Router, bun |
| Architecture | modular monolith — `*Modules.*` per capability |

The frontend monorepo at `web/` ships **two independent apps** under
`web/apps/`: `@plexor/console` (the operator UI) and `@plexor/docs`
(landing + documentation site). The apps are siblings — neither
depends on the other to run, and they share only `@plexor/ui`
(Plexor Design System: tokens, brand mark, theme picker) via a
bun workspace, not via a published package. Run them individually
with `bun run dev` (console) or `bun run dev:docs` (docs). Deploy
targets are independent — one nginx vhost per app when each gets
its own origin (future).

## Modules (planned / in progress)

- `plexor.modules.realm` — Organization / Team / Folder hierarchy
- `plexor.modules.sigil` — Users, Roles, API keys, SSH keys
- `plexor.modules.clusters` — Control plane + Node fleet
- `plexor.modules.audit` — Audit log
- `plexor.shared.kernel` — CQRS, persistence, base types

## Plans

See [`.agents/docs/plans/`](https://github.com/dot-stbl/plexor/tree/develop/.agents/docs/plans)
for design docs and roadmap.

## Contributing

Open source, work-in-progress. Open an issue if you want
to discuss something.

## License

[MIT](LICENSE)

---

<sub>built by <a href="https://github.com/dot-stbl">.stbl</a></sub>