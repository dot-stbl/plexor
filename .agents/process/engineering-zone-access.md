---
description: engineering-zone edit permissions differ by agent harness — pi-coding interactive sessions may edit with in-session owner authorization; batch and other harnesses (claude code, cursor, windsurf) need separately obtained written permission per change
priority: high
always: true
---

# Engineering-zone access — harness tiers

The engineering zone (`.agents/rules/...`-canonical list below) is **not**
uniformly off-limits. Access depends on **which harness** is running and
**whether the owner is in the loop**.

## The two tiers

| Tier | Harness | Owner in loop? | Can edit engineering zone? |
|---|---|---|---|
| **A — interactive** | **pi-coding agent** (this harness) — owner present in the session, authorising turn-by-turn | ✅ yes | ✅ **yes, when the owner authorises in-session** (verbally, via a picker answer, or by an active phase D-ZONE override) |
| **B — batch / unattended** | Claude Code, Cursor, Windsurf, CI runners, detached subagents without live owner | ❌ no | ❌ **no** — requires **separately obtained written permission** (a prior chat record, an ADR, a CODEOWNERS note) per change. Session presence alone is not authorisation |

## What "engineering zone" means (the frozen core)

Same list as `.claude/rules/02-zone-boundaries.md` §"Engineering zone — DO NOT EDIT":

- `src/shared/Hybrid.Shared.Kernel/**` · `src/shared/Hybrid.Shared.Contracts/**` (internals — constant classes in `Ports/` etc. are editable per their own sub-rules)
- `src/engine/**`
- `src/host/**/Program.cs` (the composition root of every host)
- `src/build/**` · `Directory.Build.props` · `Directory.Build.targets` · `Directory.Packages.props`
- `.editorconfig` (production) — see `coding/ANALYZERS.md` owner-approval rule
- `console.x.slnx` · `.gitlab-ci.yml` · `CODEOWNERS`
- `.agents/rules/**` · `.claude/rules/**` · `CLAUDE.md` (the rules themselves)

## Authorisation model

### Tier A — pi-coding (interactive)

The owner is in the session. Authorisation is **in-session** and may be:

1. **Explicit per-change** — owner says "yes, change X". Covers the change only.
2. **Phase D-ZONE override** — owner authorises a whole phase's engineering-zone
   touches up front (e.g. "Phase 8 D-ZONE: Program.cs of 3 hosts + new shared
   project"). Covers every task in that phase's PLAN. Logged in `.agents/STATE.md`
   decisions + the phase `CONTEXT.md`.
3. **Standing, for this repo** — none. There is no blanket "pi-coding can always
   edit engineering zone". Each non-trivial change still wants a sentence of
   owner intent on the record.

When authorised, the agent **edits directly** — it does not produce a patch for
the owner to apply, does not stop to re-ask if authorisation was already given
in this session, and does not treat the zone as forbidden by reflex. The owner
being present and saying "do it" **is** the permission.

After editing, the agent still respects:
- the build gate (`dotnet build console.x.slnx -c Debug` → 0/0) —
  `process/build-verification.md`
- the self-audit gate — `process/worker-audit.md`
- `.editorconfig` owner-approval for severity changes — `coding/ANALYZERS.md`

### Tier B — batch / other harnesses (Claude Code etc.)

The owner is **not** in the loop turn-by-turn. Engineering-zone edits require
**separately obtained written permission**: a prior chat record, an ADR, a
`CODEOWNERS` note, or a phase D-ZONE override documented **outside** the current
batch run. "I think the owner would want this" is not permission.

If a Tier-B agent hits an engineering-zone file without prior written
authorisation, it **stops** and reports — exactly the legacy "STOP and ask the
user" posture from `.claude/rules/02-zone-boundaries.md`.

## Why two tiers

The engineering zone is frozen for **batch** agents because they can't ask
questions and a wrong edit propagates before anyone sees it. An **interactive**
pi-coding session has the owner on the other side of every turn — the owner can
catch a bad edit immediately, so the cost of allowing direct edits is low and
the cost of forcing a stop-and-ask round-trip on every engineering-zone touch
(which this repo does a lot of — composition root, build tools, kernel) is high.

The build gate + arch tests + self-audit gate remain the safety net regardless
of tier: a bad engineering-zone edit fails the build before it ships.

## Good / Bad

```
# ✅ Tier A (pi-coding, owner present)
owner: "почини TEMP DIAGNOSTICS блок в targets"
agent: edits src/build/Hybrid.Build.Tools/Hybrid.Build.Tools.targets directly,
       runs build gate, commits with [hybrid](meta/build): ...

# ✅ Tier A — phase D-ZONE override already on the record
agent (executing Phase 8a Task 4): rewrites src/host/Hybrid.Host/Program.cs
       per the PLAN; does not re-ask — D-ZONE override in 08-CONTEXT.md covers it

# ❌ Tier A — no authorisation yet
agent: about to edit src/shared/Hybrid.Shared.Kernel/** on its own initiative
       → STOP, surface the proposed change, get a "yes" first

# ❌ Tier B (Claude Code batch)
agent: edits src/engine/** because "it looks like the fix"
       → violation; should have stopped and reported for separately-obtained
         written permission

# ❌ Tier A — treating zone as reflexively forbidden after authorisation
owner: "давай без агентов, правь Program.cs тут"
agent: "это engineering zone, STOP and ask"
       → wrong; owner already authorised in-session, edit directly
```

## Related

- `.claude/rules/02-zone-boundaries.md` — Claude Code's stricter (Tier-B) pointer
- `process/build-verification.md` — the single build gate (applies to both tiers)
- `process/worker-audit.md` — self-audit gate (Tier A + delegated Tier-B runs)
- `coding/ANALYZERS.md` — `.editorconfig` owner-approval (orthogonal to tier)
- `.agents/docs/ARCHITECTURE.md` §11.1 — why the core is frozen
