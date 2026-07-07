---
description: commit format is [hybrid](<feature/...>): <subject> — project tag, required feature path, imperative subject
priority: high
always: true
---

# Commit format

Every commit in this repository follows exactly one shape:

```
[hybrid](<feature/...>): <subject>
```

Three required parts, no exceptions:

| Part | Description |
|---|---|
| `[hybrid]` | Literal project tag. Identifies this repo's commits in a multi-repo workspace. |
| `(<feature/...>)` | Required feature path in parentheses. Hierarchical, lowercase kebab-case, slashes for nesting (see §Feature path). |
| `<subject>` | Imperative summary, no period, ≤72 chars, lowercase. |

**The feature path is always present** — there is no `[hybrid]: <subject>` form.
If a change spans multiple features, pick the dominant one and put the rest in
the body. A change without a clear dominant feature belongs to `meta`.

## Feature path

A feature path is **lowercase kebab-case**, slashes for nesting. The first segment
is always a top-level area; segments after are sub-features. Two to five segments
is the sweet spot.

### Top-level areas

| Feature | When |
|---|---|
| `tenants` | Changes inside `src/modules/Hybrid.Modules.Tenants/` (the only module so far) |
| `kernel` | Changes inside `src/shared/Hybrid.Shared.Kernel/` (CQRS, outbox, behaviors, base types) |
| `contracts` | Changes inside `src/shared/Hybrid.Shared.Contracts/` (public interfaces, DTOs) |
| `host` | Changes to `src/host/Hybrid.Host/` (composition root, `Program.cs`) |
| `engine` | Changes inside `src/engine/` (Phase 2+: assembly / pre-aggregation / delivery) |
| `tests` | Changes in `tests/` |
| `meta` | Build, CI, deps, scripts, repo-level config, **rules themselves** |
| `docs` | Documentation-only commits (`README.md`, `.agents/docs/`, ADRs) |

Adding a new top-level area is an architecture decision — discuss in chat first.

### Sub-paths

Append a sub-path to localize the change inside a feature:

```
[hybrid](tenants): add workspace creation endpoint
[hybrid](tenants/api): wire controllers into Program.cs
[hybrid](tenants/application/installers): explicit handler registration
[hybrid](kernel/cqrs): drain domain events into outbox
[hybrid](kernel/messaging): co-commit outbox + aggregate writes
[hybrid](engine/assembly): plugin discovery scaffolding
[hybrid](tests/architecture): assert kernel does not depend on modules
[hybrid](meta/ci): fold format-check into the build gate
[hybrid](meta/rules): rewrite commit-format rule
[hybrid](docs): add ADR-0003 for plugin discovery
[hybrid](meta/deps): bump dotnet to 10.0.108
```

### Rules for picking a feature path

1. **The first segment must be a top-level area** from the table above. No
   inventing new top-level areas without discussion.
2. **One feature path per commit.** If a commit touches `kernel` and `tests`,
   prefer the one that drives the change (usually `kernel`) and mention the rest
   in the body.
3. **Don't duplicate path segments.** `[hybrid](tenants/tenants/api)` is wrong —
   use `[hybrid](tenants/api)`.
4. **Don't use `meta` for code changes.** Build/CI/docs `meta` is for tooling
   only; code changes use a feature scope even if they touch a build script.

## Subject

- **Imperative** — "add", "fix", "bump", "wire" — not "added", "fixed", "bumped".
- **No period** at the end.
- **≤72 characters** total in the subject line.
- **Lowercase** for the subject.
- **No "wip", "tmp", "draft" markers** — if it's not ready, don't commit it.

## Body (optional)

Through a blank line after the subject. Wrap at ~72 chars. Explains **why**, not
**what** — the diff already shows what. The body is where context, trade-offs,
and risk live.

```
[hybrid](kernel/messaging): make outbox insert atomic with aggregate write

Раньше outbox insert делался отдельным SaveChanges — между ним и
aggregate write другой writer мог прочитать half-committed state.
Склеили в одну транзакцию через ChangeFeedPublish + TransactionBehavior
(см. ADR-0002 §co-commit).

Verified: 10-writer race in tests/Hybrid.ArchitectureTests.Integration/Outbox,
no duplicate / lost rows.
```

## Footer (optional)

Through a blank line after the body. For breaking changes and ticket refs:

```
[hybrid](tenants/api): change /workspaces response shape

BREAKING CHANGE: /workspaces now returns { items, total } instead of array.
Migration: clients must read .items.

Refs: COM-142
```

## Good

```
[hybrid](tenants): add workspace creation endpoint
[hybrid](tenants/application): wire create-workspace command handler
[hybrid](tenants/infrastructure): migrations for workspaces table
[hybrid](kernel/cqrs): drain domain events into outbox
[hybrid](kernel/messaging): co-commit outbox + aggregate writes
[hybrid](host): register outbox relay hosted service
[hybrid](engine/assembly): plugin discovery scaffolding
[hybrid](tests/architecture): assert kernel does not depend on modules
[hybrid](meta/ci): fold format-check into the build gate
[hybrid](meta/rules): rewrite commit-format rule
[hybrid](docs): add ADR-0003 for plugin discovery
[hybrid](meta/deps): bump dotnet to 10.0.108
```

## Bad

```
feat(tenants): add workspace endpoint              ← no [hybrid] tag, wrong type prefix
chore(format): apply fixes                         ← Conventional Commits 1.0.0, deprecated
[hybrid] add workspace endpoint                    ← feature path missing
[hybrid](tenants) add workspace endpoint           ← missing `: ` separator
[hybrid](tenants/tenants): add workspace           ← duplicate path segment
[hybrid](tenants/api): Added workspace endpoint.   ← past tense, period at end
[hybrid](tenants/api): wip                         ← WIP marker
[hybrid](PROJ): add workspace                      ← uppercase feature path
[hybrid](tenants/api): this is a long subject that exceeds the seventy-two character limit and rambles on
[hybrid](tenants/api): add Foo (only Bar exists)   ← TYPES in subject, not names
```

## Validation regex

For tooling (commit-msg hook, CI lint, etc.):

```regex
^\[hybrid\]\(([a-z][a-z0-9-]*(/[a-z][a-z0-9-]*)*)\): .{1,72}$
```

Group 1 = feature path, rest = subject. The body and footer are not checked by
the regex.

## History migration

Commits before this rule adopted `[hybrid]` use Conventional Commits 1.0.0
(`feat(scope): subject`, `fix(scope): subject`, etc.) — see `git log` from the
bootstrap era. They are **not** rewritten; the rule applies forward only. If a
future migration becomes desirable, do it via a `git rebase -i --exec`, **not**
a mass `filter-branch`.

## Overrides

This rule overrides `~/.claude/rules/git.md`, which previously required
`[stbl](<type>): <description>`. Per the soly rule hierarchy (`.agents/rules/`
beats `~/.claude/rules/`), `[hybrid](<feature/...>)` wins in this repository.
