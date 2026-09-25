# AGENTS — OpenSpec conventions for Plexor

> OpenSpec stores Plexor's spec-driven-development artifacts under
> `openspec/`. This file is the project-specific companion to the global
> OpenSpec conventions: read it before editing any spec or change file.

## Where things live

```
openspec/
├── AGENTS.md                       # this file
├── specs/
│   ├── realm/spec.md               # Organization / Team / Folder hierarchy
│   ├── identity/spec.md            # auth, RBAC, tokens, API keys
│   ├── clusters/spec.md            # control plane + nodes + workloads
│   ├── quotas/spec.md              # (Phase 4.5 — see changes/phase-4-5-quotas/)
│   └── auth-providers/spec.md      # (Phase 4.6+ — placeholder, plan in .agents/docs/plans/)
└── changes/
    └── phase-4-5-quotas/
        ├── proposal.md             # why / what / impact / out-of-scope
        ├── tasks.md                # numbered implementation checklist
        ├── design.md               # key technical decisions + rationale
        └── specs/
            ├── quotas/spec.md      # ## ADDED Requirements for the quotas capability
            └── identity/spec.md    # ## ADDED Requirements for the identity capability
```

`specs/<capability>/spec.md` = current state of truth (what the code does
today). `changes/<change-id>/specs/<capability>/spec.md` = proposed delta
that lands when the change is applied.

## Reading order for a new agent

1. `AGENTS.md` (repo root) — naming theme vs C# concept, scope hierarchy,
   migration order, build gate.
2. `.agents/HANDOFF.md` — current state, what's done, what's next.
3. `.agents/docs/scope.md` — what is in MVP and what is not.
4. `.agents/docs/architecture.md` — layers, data flow, decomposition
   strategy.
5. `openspec/specs/<capability>/spec.md` — the capability you are about
   to touch.

Do **not** edit a capability's current state by hand. Edit a change
under `openspec/changes/<change-id>/` instead.

## Spec-first workflow

When implementing a feature:

1. Read `openspec/specs/<capability>/spec.md` to understand the current
   state of the capability you are touching.
2. Read or write `openspec/changes/<change-id>/proposal.md`,
   `tasks.md`, `design.md`, and `specs/<capability>/spec.md` (the
   change-scoped delta using `## ADDED Requirements` /
   `## MODIFIED Requirements` / `## REMOVED Requirements`).
3. Implement the change in code by following `tasks.md`.
4. The capability spec at `openspec/specs/<capability>/spec.md` is
   updated only when the change is merged, by promoting the
   change-scoped delta. During a change's lifetime, the change-scoped
   delta is the source of truth.

## Spec writing rules (Plexor-specific)

- Each `### Requirement: <short name>` is one testable rule. A reader
  can answer "yes, code X does this" or "no, code X doesn't" without
  ambiguity.
- Use `SHALL` for mandatory, `MUST` for absolute, `SHOULD` for
  recommended.
- Reference concrete files and types where they exist:
  `Plexor.Shared.Authorization.RequirePermissionAttribute`,
  `Plexor.Modules.Realm.Domain.Entities.Organization`,
  `Plexor.Modules.Sigil.Domain.Entities.User`, etc. — not "see
  authorization" or "see the user entity".
- Keep specs focused on **what**, not **how**. Implementation detail
  belongs in `design.md`.
- Schema names use the architecture theme (`sigil`, `realm`, `atlas`,
  `forge`, etc.). C# concept names use the domain language
  (`Organization`, `User`, `Cluster`, `Node`). See the root
  `AGENTS.md` §"Naming: architecture theme vs C# concept" for the full
  mapping.

## Naming theme ↔ C# concept (Plexor cheat sheet)

| Schema | C# module project | Entities owned |
|--------|-------------------|----------------|
| `sigil` | `Plexor.Modules.Sigil` | `User`, `Role`, `RoleBinding`, `ApiKey`, `SshKey`, `RefreshToken`, `SigningKey` |
| `realm` | `Plexor.Modules.Realm` | `Organization`, `Team`, `Folder` |
| `atlas` | `Plexor.Modules.Audit` | `AuditEntry` |
| `forge` | `Plexor.Modules.Clusters` | `Cluster`, `Node`, `Workload`, `JoinToken`, `NodeCommand` |

Schema name in SQL or migration. C# class name in code. Never both.

## Build + verification gate

```bash
dotnet build plexor.slnx -c Debug
```

Build must be clean (0 warnings, 0 errors). Three things gate in one
command:

1. **Compilation** — TypeScript/C# errors fail the build.
2. **Analyzers** — CA / RCS / IDE / VSTHRD via `EnforceCodeStyleInBuild`
   + `TreatWarningsAsErrors=true` in `Directory.Build.props`.
3. **Format drift** — `VerifyFormatOnBuild` target runs
   `dotnet format plexor.slnx --verify-no-changes --severity hidden`.

Spec edits are pure markdown — no build needed. But if the spec change
ships alongside code, run the gate before committing.

## Commit format

```
[.stbl](feat/<area>): <subject>
```

See `~/.agents/rules/process/commit-format.md`. Never `[app]`, never
`feat:` without the `.stbl` tag.

For OpenSpec-only changes (proposal / tasks / design / spec delta) the
area is `feat/meta/specs` or `feat/<capability>/<change-id>`. Pick the
area that names where the implementation will land.

## Worktree convention

New feature / fix work runs in an isolated worktree:

```bash
git worktree add .agents/worktree/<branch-slug> \
  -b feature/<branch-slug> origin/<integration>
```

Branch prefix: **`feature/`**, **`fix/`**, or **`test/`** — full words.
**Never `feat/`** for branch names (collision with the commit-format
area `feat/`).

The main checkout stays on the integration branch and is used for
status / triage / merge only.

## Engineering zone — files that need explicit authorization

The following files are off-limits unless the user has authorized
the edit in-session:

- `AGENTS.md` (repo root)
- `.agents/HANDOFF.md`
- `Directory.Build.props`
- `Directory.Build.targets`
- `Directory.Packages.props`
- `.editorconfig`
- `plexor.slnx`
- `openspec/AGENTS.md` (this file)
- any `~/.agents/rules/*.md`

In an interactive session, the user explicitly authorizes an edit by
naming the file ("fix the build targets", "update commit-format").
For batch / unattended runs, edit without prior written authorization
is a violation.

## Cross-references

- `AGENTS.md` (root) — naming theme, scope hierarchy, migration order.
- `.agents/HANDOFF.md` — current state, recent work, conventions.
- `.agents/docs/architecture.md` — layers and decomposition strategy.
- `.agents/docs/architecture/identity.md` — auth, RBAC, JWT, API keys.
- `.agents/docs/plans/plan-auth-providers.md` — auth-providers plan
  (NOT being implemented in Phase 4.5; placeholder only).
- `src/modules/Plexor.Modules.Realm/Plexor.Modules.Realm.Domain/Entities/{Organization,Team,Folder}.cs`
  — Realm entity definitions.
- `src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Domain/Entities/{User,Role,RoleBinding}.cs`
  — Sigil entity definitions.
- `src/shared/security/Plexor.Shared.Authorization/RequirePermissionAttribute.cs`
  — the cross-cutting authorization attribute.