# ADR-0001: Selective Decomposition — planned extraction of high-load modules

- **Status:** Accepted
- **Date:** 2026-07-12
- **Decision driver:** bradw (owner)
- **Supersedes:** —

## Context

Plexor uses a **modular monolith** architecture: one `Plexor.Host`
binary contains all modules (Tenants, Identity, Compute, Network,
Storage, Billing, Telemetry, Marketplace). This is deliberate for MVP —
single binary, single deployment, fastest time-to-market.

Three modules are predicted to hit scale walls at SMB-mid scale and
beyond:

- **Audit** (currently sub-concern of Telemetry module) — write-heavy,
  7-year retention, compliance-driven, append-only `audit_log` table.
  Burst pattern at operation peaks (e.g. mass VM termination during
  auto-scaling events).
- **Telemetry** — OTel collector, metrics scrape, log aggregation.
  Cardinality spikes when a noisy customer emit pattern emerges.
- **Network** — SDN control plane state machine (VPCs, subnets,
  security groups, floating IPs, load balancers). State explosion risk
  when a tenant runs 10K+ networks or churns SG rules at high QPS.

A naive "microservice everything" path — à la OpenStack's 30 services
(Nova, Neutron, Cinder, Glance, Keystone, …) — would create
operational overhead (CI pipelines, distributed tracing, mTLS,
distributed schema migrations) that would kill the project at SMB
scale.

Counter-evidence from production systems shows monoliths serve
billions: GitHub (Rails), Shopify (Rails, "Monolith Manifesto"),
Stack Overflow (ASP.NET), Discourse (Rails), Basecamp (Ruby). The
common trait is operational maturity, not monolithic architecture.

## Decision

We adopt an **Extraction-ready Hybrid** pattern.

### Phase 1 (MVP, today)

- **Modular monolith** — all modules in `Plexor.Host` single binary.
- **No premature microservices** — even Audit/Telemetry/Network run in
  the same process as Compute/Identity/Tenants.
- Modules communicate via:
  - **In-process calls** through primary-ctor injection for
    non-critical paths (read-only lookups, sync queries).
  - **Outbox events** (`Plexor.Shared.Kernel.Outbox`) for cross-module
    async (audit emit, metering emit, telemetry emit).

### Phase 2+ (when load warrants)

**Planned extraction** of three modules to separate binaries, each with
its own deploy lifecycle:

| Module | New binary | Predicted trigger |
|---|---|---|
| Audit (sub-concern of Telemetry today) | `Plexor.Audit.Host` | Write > 100K events/s, retention > 1TB, OR regulatory isolation need |
| Telemetry | `Plexor.Telemetry.Host` | p99 OTel ingest > 500ms OR CPU > 70% sustained |
| Network | `Plexor.Network.Host` | State > 100K networks OR scheduler contention with other module ops |

**Extraction = deploy change, not refactor.** Each module is designed
from day 1 to be deployable as its own binary without code rewrite.

### Extraction-ready design rules (already enforced or being added)

1. **Module contracts in `Plexor.Shared.Contracts`** — every cross-module
   interface goes here. Both monolith deploy and split deploy use the
   same assembly.
2. **No `ModuleA.Infrastructure → ModuleB.Domain`** dependencies.
   Cross-module reads go through `ModuleB.Contracts` interface only.
3. **Per-module DbContext + per-module schema** (already enforced).
   Schemas are pre-named: `audit`, `telemetry`, `network` so extraction
   = move schema to new DB if needed.
4. **Cross-module async via outbox** (`plexor.outbox` table in shared
   schema or per-module). The same outbox row is read by in-process
   subscriber today, by separate binary via `SELECT ... FOR UPDATE
   SKIP LOCKED` after extraction.
5. **Module diagnostics interface** in `Plexor.Shared.Telemetry` —
   `IAuditDiagnostics`, `INetworkDiagnostics`, `ITelemetryDiagnostics`
   so observability doesn't break when extracted.
6. **CLI subcommands per module** — `plx audit query`, `plx network vpc list`,
   etc. The CLI talks to whichever binary exposes the endpoint; in
   monolith it's the same host, in split it's separate URLs.

### When NOT to extract

- Don't extract anything beyond the 3 listed (Audit, Telemetry, Network)
  unless Phase 3+ data demands it.
- Don't extract for "purity" / "cleanness" — extraction is
  operational burden, not a virtue.
- Don't extract Identity, Tenants, Compute, Storage, Billing,
  Marketplace — these are not predicted to be hot-path and extraction
  is not justified.

## Rationale

### Why not full microservices (OpenStack-style 30+ services)

- Each service = +1 deploy, +1 CI pipeline, +1 schema migration, +1
  monitoring target, +1 secret rotation, +1 CVE patch cycle.
- Mandatory: distributed tracing, mTLS, retries, circuit breakers,
  service discovery, eventually-consistent sagas.
- Local dev becomes docker-compose / k8s-only — kills 5-person team
  velocity.
- Counter-evidence: GitHub, Shopify, Stack Overflow all served
  billions on monolith.

### Why not pure monolith (forever)

- Write-heavy modules (audit, telemetry) risk overloading control plane
  DB at scale — affects all API latency.
- Networking state explosion can affect control plane scheduler
  contention with VM operations.
- Independent scaling of hot paths becomes necessary at SMB-mid scale.
- Pure monolith predication breaks when load profile of one module
  diverges from others.

### Why Extraction-ready Hybrid

- **Zero rework** when extraction time comes — design boundary is
  already there from day 1.
- **Local dev stays fast** — in-process calls for non-critical paths.
- **Performance** for critical paths — direct in-process, no HTTP
  overhead.
- **Future flexibility** — extract *when bottleneck proven*, not
  before.

## Consequences

### Positive

- Single binary to operate today (MVP-friendly, `plx init` simplicity).
- No 30-service operability burden.
- Extraction is **deploy change**, not refactor.
- Independent scaling of audit / telemetry / networking in Phase 2+.
- Compliance isolation: `Plexor.Audit.Host` can have its own retention
  policy, SOC 2 evidence chain, separate credentials.

### Negative

- **Upfront cost**: every module needs `IPort` interface in
  `Shared.Contracts` even when only used in-process.
- **DB schemas need discipline** (already enforced via
  `Plexor.Shared.Persistence`).
- **Outbox event volume adds to control plane DB size** — must be
  monitored and pruned.
- **3 binary deploy path** in Phase 2+ — operators run 1 binary today,
  3-4 binaries later. CLI/UI must abstract this.

### Risks

- **Over-extraction** — pressure to extract "just because". Mitigation:
  Rule is "only on measured bottleneck (p99 > 500ms or CPU > 70%
  sustained), with repl discussion in `.agents/STATE.md`".
- **Under-extraction** — leaving audit/telemetry/network in control
  plane when load warrants extraction. Mitigation: SLO monitoring with
  alerts on ingest latency, write throughput, state count.
- **Premature abstraction** — adding `IPort` everywhere "in case we
  extract". Mitigation: only on cross-module touchpoints (≥2 modules
  consume the interface); internal-module helper classes stay
  private.

## Alternatives considered

### A. Strict microservices from day 1

All modules as separate processes from day 1 (HTTP + outbox only).

| | Pros | Cons |
|---|---|---|
| | Predictable deploy topology from start | +30% dev overhead, +100% CI complexity |
| | Each module scales independently | Harder debugging (distributed traces mandatory) |
| | | docker-compose / k8s required for local dev |
| | | No benefit until load is proven |

**Rejected** — premature operational cost without proven need.

### B. Lazy monolith (no upfront boundary design)

Write all modules with free in-process access. Refactor when extraction
time comes.

| | Pros | Cons |
|---|---|---|
| | Simplest today | Extraction = 4-week refactor per module |
| | | Data migration, test rewrite, contract negotiation |
| | | Extraction is *predictable* (we know which modules are hot) — design for it now |

**Rejected** — known hot-path modules (Audit, Telemetry, Network) are
predicted by domain knowledge, not just metrics. Designing boundary now
costs us ~5% in upfront contract declarations; designing it later costs
~30% in refactor.

### C. Extract immediately (write all 3 services in MVP)

`Plexor.Audit.Host`, `Plexor.Telemetry.Host`, `Plexor.Network.Host`
from day 1.

| | Pros | Cons |
|---|---|---|
| | No migration later (already split) | 3x deploy complexity day 1 |
| | | 3x CI/day, 3x schemas, 3x observability |
| | | Dev velocity 3x slower (every VM operation = cross-network call) |
| | | No proven load to justify it |

**Rejected** — building for scale we can't measure yet. Premature
extraction is the OpenStack anti-pattern in miniature.

## References

- `.agents/docs/architecture.md` §Selective Decomposition — high-level
  summary, link back to this ADR.
- `.agents/docs/modules.md` §Extraction Tier — per-module extraction
  tier and trigger metric.
- `.agents/docs/scope.md` §Phase 2+ — pointer from existing Phase 2+
  table.
- OpenStack Nova cells (inspiration for selective decomposition when
  load demands — see also [Nova cells documentation](https://docs.openstack.org/nova/latest/user/cells.html)).
- Shopify ["Monolith Manifesto"](https://shopify.engineering/shopify-monolith/) —
  inspiration for monolithic-first, then surgical extraction.

## Open questions (track in `.agents/STATE.md` decisions)

- **Q1**: Should `Plexor.Audit.Host` and `Plexor.Telemetry.Host` share
  a deploy artifact (one binary, two modules) or stay fully separate?
  Hypothesis today: separate (compliance isolation beats deploy
  convenience for audit). Revisit at extraction time.
- **Q2**: When Network is extracted, do we keep the data-plane agent
  (Plexor.NodeAgent) talking to `Plexor.Network.Host` directly, or
  route through `Plexor.Host`?
  Hypothesis today: direct (lower latency for VXLAN setup/teardown).
  Revisit at extraction time.
- **Q3**: Does `Plexor.Network.Host` need its own DB or can it share
  the control plane DB with new `network` schema only?
  Hypothesis today: shared DB, own schema (lower ops cost). Revisit
  if write contention measured.
