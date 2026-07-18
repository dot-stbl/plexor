<div align="right">

<img src="https://raw.githubusercontent.com/dot-stbl/.github/main/assets/wordmark.svg" alt="" width="120">

</div>

# Plexor

**self-hosted cloud platform**

---

A modular monolith for self-hosted infrastructure.

Plexor gives you cloud-like ergonomics on your own hardware —
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

## Modules (planned / in progress)

- `Plexor.Modules.Realm` — Organization / Team / Folder hierarchy
- `Plexor.Modules.Sigil` — Users, Roles, API keys, SSH keys
- `Plexor.Modules.Clusters` — Control plane + Node fleet
- `Plexor.Modules.Audit` — Audit log
- `Plexor.Shared.Kernel` — CQRS, persistence, base types

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