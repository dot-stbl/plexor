---
description: commit format is [.stbl](<feat/...>): <subject> — project tag, required feature path, imperative subject
priority: high
always: true
---

# Commit format

Every commit in this repository follows exactly one shape:

```
[.stbl](<feat/...>): <subject>
```

Three required parts, no exceptions:

| Part | Description |
|---|---|
| `[.stbl]` | Literal project tag. Identifies this repo's commits in a multi-repo workspace; the `.stbl` prefix means "stbl.solutions project". |
| `(<feat/...>)` | Required feature path in parentheses. Always starts with `feat/` followed by a hierarchical, lowercase kebab-case area (see §Feature path). |
| `<subject>` | Imperative summary, no period, ≤72 chars, lowercase. |

**The feature path is always present** — there is no `[.stbl]: <subject>` form.
If a change spans multiple features, pick the dominant one and put the rest in
the body. A change without a clear dominant feature belongs to `feat/meta`
(rules, build, CI, scripts, repo-level config — see §Top-level areas).

## Feature path

A feature path is **lowercase kebab-case**, slashes for nesting. The first segment
is **always** `feat/`; segments after are the area name. Two to five segments is
the sweet spot.

```
[.stbl](feat/<area>): <subject>
[.stbl](feat/<area>/<sub-area>): <subject>
```

### Top-level areas

Top-level areas for Plexor (driven by the project layout — `src/modules/*`,
`src/agents/*`, `src/host/*`, `src/installer/*`, `src/shared/*`, plus
tooling/CI/docs):

| Area | When |
|---|---|
| `feat/clusters` | Changes inside `src/modules/Plexor.Modules.Clusters/` (Cluster + Node + Workload + Heartbeat) |
| `feat/sigil` | Changes inside `src/modules/Plexor.Modules.Sigil/` (Identity: Users, Roles, API keys, refresh tokens, signing keys) |
| `feat/realm` | Changes inside `src/modules/Plexor.Modules.Realm/` (Organization + Team + Folder hierarchy) |
| `feat/audit` | Changes inside `src/modules/Plexor.Modules.Audit/` (AuditEntry writes) |
| `feat/compute` | Changes inside `src/shared/kernel/Plexor.Shared.Compute/` (IVolumeBackend / IImageRegistry / INetworkBackend interfaces) |
| `feat/workloads` | Changes inside `src/shared/kernel/Plexor.Shared.Workloads/` (IWorkloadProvider contract) |
| `feat/contracts` | Changes inside `src/shared/kernel/Plexor.Shared.Contracts/` (public interfaces, DTOs, ApiRoutes) |
| `feat/kernel` | Changes inside `src/shared/kernel/Plexor.Shared.Kernel/` (CQRS, outbox, behaviors, base types) |
| `feat/host` | Changes to `src/host/Plexor.Host/` (composition root, controllers, mTLS, cert authority) |
| `feat/nodeagent` | Changes inside `src/agents/Plexor.NodeAgent/` (NodeAgent runner, libvirt/providers/, compute backends) |
| `feat/installer` | Changes inside `src/installer/` (CLI installer + os/engine providers) |
| `feat/fe` | Changes inside `web/` (Plexor Portal monorepo) |
| `feat/meta` | Build, CI, deps, scripts, repo-level config, **rules themselves** |
| `feat/docs` | Documentation-only commits (`.agents/docs/`, ADRs, README updates) |

**All commits start with `feat/`** — `feat/` is the universal first segment.
The second segment picks the area. Sub-segments localize further.

### Sub-paths

Append a sub-path to localize the change inside an area:

```
[.stbl](feat/clusters): add workload action endpoint
[.stbl](feat/clusters/api): wire controllers into Program.cs
[.stbl](feat/clusters/application/installers): explicit handler registration
[.stbl](feat/runtime-providers/tier-3): docker compose provider
[.stbl](feat/runtime-providers/tier-5): k3s provider + kustomize renderer
[.stbl](feat/nodeagent): adopt compute abstractions
[.stbl](feat/kernel/cqrs): drain domain events into outbox
[.stbl](feat/host/mtls): end-to-end mtls with pem certs
[.stbl](feat/tests/architecture): assert kernel does not depend on modules
[.stbl](feat/meta/ef): regenerate migrations via dotnet ef tool
[.stbl](feat/meta/rules): rewrite commit-format rule
[.stbl](feat/meta/format): cleanup pre-existing format drift
[.stbl](feat/docs): add ADR-0003 for plugin discovery
[.stbl](feat/meta/deps): bump dotnet to 10.0.108
```

### Rules for picking a feature path

1. **The first segment is always `feat/`.** No `[.stbl](clusters): ...` —
   the second segment picks the area.
2. **The second segment must be a top-level area** from the table above. No
   inventing new top-level areas without discussion in chat.
3. **One feature path per commit.** If a commit touches `feat/clusters` and
   `feat/tests`, prefer the one that drives the change (usually
   `feat/clusters`) and mention the rest in the body.
4. **Don't duplicate path segments.** `[.stbl](feat/clusters/clusters/api)`
   is wrong — use `[.stbl](feat/clusters/api)`.
5. **Don't use `feat/meta` for code changes.** `feat/meta` is for tooling,
   rules, CI, build, scripts. Code changes use a feature area even if they
   touch a build script — e.g. `feat/nodeagent` for csproj edits in
   `src/agents/Plexor.NodeAgent/`.

### When no clear area fits

A pure docs change with no implementation impact → `feat/docs`. A
repo-config / rules / build / CI change → `feat/meta`. Anything that
primarily changes a runtime area → that area. If two areas tie, pick the
one that's downstream of the other and mention the upstream in the body.

## Subject

- **Imperative** — "add", "fix", "bump", "wire" — not "added", "fixed", "bumped".
- **No period** at the end.
- **≤72 characters** total in the subject line.
- **Lowercase** for the subject.
- **No "wip", "tmp", "draft" markers** — if it's not ready, don't commit it.
- **File names in subject = OK** (e.g. `fix Dockerfile`, `wire OpenApiBuildTimeExtensions`).
  **Type names in subject = bad** — names go in code, commit messages describe
  intent.

## Body (optional)

Through a blank line after the subject. Wrap at ~72 chars. Explains **why**, not
**what** — the diff already shows what. The body is where context, trade-offs,
and risk live.

```
[.stbl](feat/kernel/messaging): make outbox insert atomic with aggregate write

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
[.stbl](feat/clusters/api): change /workspaces response shape

BREAKING CHANGE: /workspaces now returns { items, total } instead of array.
Migration: clients must read .items.

Refs: COM-142
```

## Good

```
[.stbl](feat/clusters): add workload action endpoint
[.stbl](feat/clusters/application): wire create-workload command handler
[.stbl](feat/clusters/infrastructure): migrations for workloads table
[.stbl](feat/runtime-providers/tier-3): docker compose provider
[.stbl](feat/runtime-providers/tier-4): podman quadlet provider
[.stbl](feat/runtime-providers/tier-5): k3s provider + kustomize renderer
[.stbl](feat/nodeagent): adopt compute abstractions
[.stbl](feat/host/mtls): end-to-end mtls with pem certs
[.stbl](feat/host/cert-authority): plexor ca + x509 issuer
[.stbl](feat/kernel/cqrs): drain domain events into outbox
[.stbl](feat/kernel/messaging): co-commit outbox + aggregate writes
[.stbl](feat/tests/architecture): assert kernel does not depend on modules
[.stbl](feat/meta/ef): regenerate migrations via dotnet ef tool
[.stbl](feat/meta/rules): rewrite commit-format rule
[.stbl](feat/meta/format): cleanup pre-existing format drift
[.stbl](feat/docs): add ADR-0003 for plugin discovery
[.stbl](feat/meta/deps): bump dotnet to 10.0.108
[.stbl](feat/fe/clusters): wire clusters screen to control-plane api
```

## Bad

```
feat(clusters): add workload endpoint             ← no [.stbl] tag, no feat/ prefix
chore(format): apply fixes                       ← Conventional Commits 1.0.0, deprecated
[hybrid](clusters): add workload endpoint        ← old [hybrid] tag, missing feat/
[.stbl] add workload endpoint                    ← feature path missing entirely
[.stbl](clusters) add workload endpoint          ← missing feat/ prefix
[.stbl](feat/clusters) add workload endpoint     ← missing `: ` separator
[.stbl](feat/clusters/clusters): add workload   ← duplicate path segment
[.stbl](feat/clusters/api): Added endpoint.      ← past tense, period at end
[.stbl](feat/clusters/api): wip                 ← WIP marker
[.stbl](feat/PROJ): add workload                ← uppercase feature path
[.stbl](feat/clusters/api): this is a long subject that exceeds the seventy-two character limit and rambles on
[.stbl](feat/clusters/api): add WorkloadActionCommand   ← TYPE names in subject
```

## Validation regex

For tooling (commit-msg hook, CI lint, etc.):

```regex
^\[.stbl\]\(feat/([a-z][a-z0-9-]*(/[a-z][a-z0-9-]*)*)\): .{1,72}$
```

Group 1 = feature path (without the leading `feat/`), rest = subject. The body
and footer are not checked by the regex.

## History migration

- **Bootstrap era** (before this rule): commits used Conventional Commits 1.0.0
  (`feat(scope): subject`, `fix(scope): subject`). **Not rewritten** — the
  rule applies forward only.
- **`[hybrid]` era**: commits used `[hybrid](<area>): subject` without a
  `feat/` prefix. Many Plexor commits from 2026-05 to 2026-07 are in this
  form. **Not rewritten** — forward only. If a future migration becomes
  desirable, do it via `git rebase -i --exec`, **not** a mass `filter-branch`.

## Overrides

This rule overrides `~/.claude/rules/git.md`, which previously required
`[stbl](<type>): <description>`. Per the soly rule hierarchy
(`.agents/rules/` beats `~/.claude/rules/`), `[.stbl](<feat/...>)` wins in this
repository.

## Other places this format appears

If you copy a snippet from these rules or a previous commit, double-check
the format. Common places `[hybrid]` or `feat/` sneaks in:

- `.agents/rules/coding/analyzers.md` §"Что делать при новых warnings" —
  example commit
- `.agents/rules/coding/dsp-gap-handling.md` §"Commit per commit-format.md"
- `.agents/rules/process/build-verification.md` §"Good / Bad" — example
  commit in git command

If you find a `[hybrid](<area>)` reference in any `.agents/rules/*.md` file
that's NOT in `commit-format.md` (this file), update it to
`[.stbl](feat/<area>)`. Format drift applies to docs too.
