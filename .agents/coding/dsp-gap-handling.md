---
description: dsp gap handling — when Alliance DSP endpoint is missing, propose addition via contracts/dsp-tz.md, do not pollute shared IDspClient with workarounds
priority: high
globs: ["src/shared/Hybrid.Shared.Http/Dsp/**/*.cs", "src/modules/*/Hybrid.Modules.*.Application/**/*.cs", "src/engine/Hybrid.Engine.*/**/*.cs"]
---

# DSP gap handling — when Alliance DSP endpoint is missing

> **Scope:** code paths in `Hybrid.Shared.Http/Dsp/` (the `IDspClient` surface),
> any caller of `IDspClient` (engine workers, billing workers, future modules),
> and the upstream artifacts in `contracts/` (the gap doc, the OpenAPI spec).
>
> **Pre-condition:** `cross-service-communication.md` already establishes that
> `IDspClient` is the **only** DSP client in the codebase (single connection
> pool, single resilience pipeline, single configuration section). This rule
> extends that with the **gap workflow**: what to do when `IDspClient` does
> not yet expose the endpoint we need.

## Why this rule exists

Alliance DSP is an **internal product** owned by a sister team. Their API is
described by `contracts/dsp-openapi.json` (~500 endpoints, NSwag 14). It is
large but not exhaustive — it does not yet cover every operation Hybrid
needs (notably a dedicated `PUT /api/DspAdvertisers/{id}/balance` for billing
balance sync, see `contracts/dsp-tz.md` P0).

Two temptations arise when a needed endpoint is missing:

1. **Cramming the operation into the nearest existing endpoint.** Example: the
   current Billing `SendBalanceToDspWorker` posts the wallet balance through
   `DspAdvertisers/AddOrUpdateMany` (a metadata endpoint that also accepts a
   balance field). It works today but pushes 2 KB on every credit/debit
   instead of ~100 B; race conditions (no `If-Match`); no idempotency key.
2. **Adding a private caller-side HTTP client.** Example: `IDspBalanceClient`
   was deleted because it duplicated `IDspClient`. A bespoke client would
   re-introduce a second connection pool + resilience pipeline + config
   section, violating `cross-service-communication.md`.

Both temptations are wrong. The right answer is to **ask the Alliance team
to add the endpoint**.

## The workflow

```
Caller code needs DSP behavior X
         │
         ▼
Does IDspClient (Hybrid.Shared.Http.Dsp) expose X?
         │
       Yes ──▶ Use IDspClient directly. Done.
         │
        No
         │
         ▼
Is X a metadata update that fits an existing endpoint
(e.g. balance piggy-backed on AddOrUpdateMany)?
         │
       Yes ──▶ ⚠ STOP. Do NOT piggy-back. Treat as "missing" and go to step
              "No" below. Piggy-backing is the old workaround; we are
              deprecating it (see contracts/dsp-tz.md P0).
         │
        No (or "Yes but I chose not to")
         │
         ▼
Add an entry to contracts/dsp-tz.md:
  - HTTP verb + path
  - Request body shape
  - Response shape + status codes
  - Why we need it (call volume, payload size, race risk)
  - Suggested priority (P0/P1/P2/P3, matching the TЗ convention)
         │
         ▼
Surface the new entry to the owner (in chat or via owner-channel).
The owner hands contracts/dsp-tz.md to the Alliance team —
**we do not open tickets with Alliance directly**.
         │
         ▼
In code, add a TODO marker at the call site referencing the TЗ entry:
  // TODO(dsp): replace with PUT /api/DspAdvertisers/{id}/balance
  //            when Alliance ships it. See contracts/dsp-tz.md P0.
  await dspClient.AddOrUpdateManyAsync(batchRequest, ct);
         │
         ▼
When Alliance ships the endpoint:
  - Add the new method to IDspClient (one call site, one ResilienceHandler
    reuses the same connection pool — see cross-service-communication.md).
  - Replace the TODO call site.
  - Strike the entry from contracts/dsp-tz.md (move to a "Shipped" section
    at the bottom for traceability).
  - Commit per commit-format.md ([hybrid](shared/dsp): ...).
```

## What this rule forbids

| Anti-pattern | Why forbidden |
|---|---|
| Adding a new `IDspXxxClient` interface for a "missing" operation | Duplicates `IDspClient` infra (pool, resilience, config); see `cross-service-communication.md` |
| Calling `new HttpClient()` inline against Alliance | Bypasses Refit + ResilienceHandler + Bearer auth; same outcome as above |
| Piggy-backing on a metadata endpoint (`AddOrUpdateMany`) when the operation is logically distinct (balance sync, partial update) | Works today but bloats payload, races with metadata updates, no idempotency key, no ETag — exactly the gaps P0-P3 in `contracts/dsp-tz.md` are meant to close |
| Skipping `contracts/dsp-tz.md` and opening an Alliance ticket directly | Ownership boundary — the owner is the channel to sister teams; agents don't bypass |

## What this rule allows

| Pattern | Why OK |
|---|---|
| Calling an existing `IDspClient` method that already covers X | Single client, single config — the rule's whole point |
| Adding a new method to `IDspClient` for an endpoint Alliance already ships but we don't yet call | Adds a Refit binding; no new infra. Cover the same `IDspClient` interface (no second client) |
| Reusing an existing endpoint with a documented dual purpose (e.g. `AddOrUpdateMany` accepts both metadata and balance) **with** a `// TODO(dsp):` marker at the call site | Explicit gap acknowledgment + TЗ reference; matches the workflow above |

## The TЗ (`contracts/dsp-tz.md`)

The TЗ is the **single source of truth** for "what we wish Alliance would
add". It already exists, but its entry order matters:

| Priority | Meaning | When to use |
|---|---|---|
| **P0** | Blocker for production correctness or scale | Today's `PUT /balance` — current piggy-back is wrong-shaped for billing scale |
| **P1** | Material improvement (race protection, payload size) | PATCH + ETag/If-Match |
| **P2** | Reliability feature (idempotency) | `Idempotency-Key` header on POST batch |
| **P3** | Latency/cost optimization | Change feed (`GET /Sync/changes?since=`); gzip + ETag/304 |

When adding a new entry, follow the existing TЗ format (HTTP example +
rationale + scope note). The owner is the one who prioritises and forwards
to Alliance.

## Enforcement

- **Code review:** any PR that touches `Hybrid.Shared.Http.Dsp` or any
  caller of `IDspClient` and adds a new operation pattern must reference
  `contracts/dsp-tz.md` (either an existing entry, or a new one in the
  same PR).
- **Architecture tests:** none today. The rule is enforced by reviewer
  attention. If violations repeat, add a Roslyn analyzer that flags
  `IDspClient` callers without a `// TODO(dsp):` marker when the called
  method is a known dual-purpose endpoint (e.g. `AddOrUpdateMany` for
  balance sync specifically).
- **Build gate:** no enforcement. The build doesn't know about TЗ gaps.

## Related

- `cross-service-communication.md` — `IDspClient` is the only DSP client
- `contracts/dsp-tz.md` — the gap document this rule maintains
- `contracts/dsp-endpoints-usage.md` — what we already call
- `contracts/dsp-openapi.json` — Alliance's surface (NSwag 14)
- `coding/anti-patterns.md` §"Endpoint-specific dependencies" — same
  principle (no client-per-operation)