# Plexor docs — content plan

> **Deliverable for "design the full document tree".** This file is
> planning only — it produces a single coherent blueprint, not pages.
> Authoring MDX content is the next task.
>
> **Source of truth.** Every page in §3 cites the spec or doc file that
> feeds its content. Page boundaries align with the **operator-visible
> surface** (the console's `nav-config.tsx` shape), not with internal
> module names or schema names. Schema names (`sigil`, `realm`, `atlas`,
> `forge`, `outpost`, `shard`) and module project paths
> (`Plexor.Modules.*`) stay out of user docs by design — developers
> have `AGENTS.md` and `openspec/`.

---

## 1. Document philosophy

### Audience

**Self-hosted operators** of Plexor — the person who installed
Plexor on their hardware and now runs day-to-day operations. Not end
users of an app deployed on Plexor. Not developers building Plexor.
Not enterprise-IT buyers.

The default persona is "small team / single host / cloud-hybrid
lab": 5–15 people, one or two hosts, running Plexor instead of
reaching for AWS. Same shape as Coolify's audience, CasaOS's
audience, YunoHost's audience, and Proxmox's home-lab audience.

### Voice

Operator-first, not engineer-first. State *what it does and when to
use it*. Avoid framework references. Avoid "we use X" justifications
unless they directly tell the operator how to recover a broken state.

See `~/.claude/skills/synced/.../user-docs/SKILL.md` (the
`user-docs` skill) for the full craft — Diátaxis shape per page
type, RU/EN parity, parameter tables, terminal session logs, etc.

### Page count and shape

**~35 pages**, grouped into six chapters following Diátaxis:

| Chapter | Diátaxis role | Page count |
|---|---|---|
| **Getting started** | Tutorials (orientation, "first successful run") | 4 |
| **Concepts** | Explanation (mental models) | 8 |
| **How-to** | How-to guides (task-oriented recipes) | 12 |
| **Admin guide** | Reference, organised by lifecycle (operations + hardening) | 6 |
| **Reference** | Reference (auto-generated + hand-curated) | 3 |
| **FAQ / Troubleshooting** | Symptom → cause → fix | 2 |

Plus the existing landing page (`/`) and the docs index (already
implied by the sidebar).

### What lives where

- `web/apps/www/src/routes/(docs)/docs/<chapter>/<slug>.mdx` —
  the canonical page. URL is `/docs/<chapter>/<slug>` when the
  chapter folder is non-trivial, or `/docs/<slug>` when it isn't
  (e.g. `/docs/getting-started` already exists as a TSX).
- `web/apps/www/src/routes/(docs)/docs/<slug>.tsx` — short prose
  pages with no MDX components (TSX with prose classes). Existing
  example: `getting-started.tsx`.
- Chapter index pages live at
  `web/apps/www/src/routes/(docs)/docs/<chapter>/index.tsx` —
  short landing for each chapter with a card grid of pages.
- Screenshots live at
  `web/apps/www/public/screenshots/<chapter>/<page>/<n>.png` —
  served at `/screenshots/<chapter>/<page>/<n>.png`.

---

## 2. Chapter list

### Ch 01 — Getting started

**Purpose:** get a brand-new operator to a working Plexor in under
30 minutes. The four pages are read in order; each one assumes the
previous.

### Ch 02 — Concepts

**Purpose:** the mental models. Reads non-linearly — the operator
lands here from a how-to and goes back to a concept page when they
need a name for what they're looking at. Pages describe Plexor's
behaviour, not its internals.

### Ch 03 — How-to

**Purpose:** task-oriented recipes. Each page is short and
goal-driven ("Install WordPress", "Attach a volume to a VM"). Ops
typically arrive via search.

### Ch 04 — Admin guide

**Purpose:** operations and hardening. Read by admins and operators
keeping the deployment healthy: backups, upgrades, monitoring,
access-control policies, audit hygiene.

### Ch 05 — Reference

**Purpose:** exhaustive catalogs and machine-readable specs the
operator may consult but rarely reads cover-to-cover. Auto-generated
endpoints + a hand-curated permission catalog + CLI reference.

### Ch 06 — FAQ / Troubleshooting

**Purpose:** symptom → cause → fix recipes. The first place an
operator searches when something is on fire.

---

## 3. Full page inventory

Columns:

- **Title** — operator-visible sidebar label.
- **URL slug** — `…/docs/…` (chapter folder optional for top
  pages; see each row).
- **Purpose** — one sentence.
- **Source of truth** — which spec / doc the page's content comes
  from.
- **Screenshots** — which console screens. **"needs capture"** =
  no screenshot exists; do not invent.
- **MDX components needed** — `<Callout>`, `<Tabs>`, etc.
- **Length** — terse ≈ ≤80 lines / medium ≈ 200 / long ≈ 400+.

**MVP boundary reminder:** a feature that isn't in an openspec spec
**is not documented**. Pages like "managed Postgres" or "managed
Redis" exist in the console nav but the spec for them isn't open yet
— see the "Soon" badge in the nav. Those routes are listed below
**only in the roadmap section, not in the live inventory**.

---

### Chapter 01 — Getting started (4 pages)

| # | Title | Slug | Purpose | Source | Screenshots | Components | Length |
|---|-------|------|---------|--------|-------------|-----------|--------|
| 1.1 | Welcome to Plexor | `/docs/getting-started` *(existing TSX)* | Frame what Plexor is, what it does for you, and what to read next. The only existing page; align voice with the rest of the docs. | `openspec/specs/realm/spec.md` + the public landing copy | None — text-only orientation. | (TSX) | Terse |
| 1.2 | First login and the admin user | `/docs/getting-started/first-login` | Get the operator into the console for the first time, set the admin password, and see what's there. | `openspec/specs/identity/spec.md` §"Local email + password authentication" + the console's login screen | `login-screen.png`, `console-shell-with-scope-switcher.png` (needs capture) | `<Callout type="info">` | Medium |
| 1.3 | Create your first organization, team, and folder | `/docs/getting-started/create-scope` | Walk through the Org → Team → Folder hierarchy interactively. The first thing an operator does that touches the domain model. | `openspec/specs/realm/spec.md` §"Resource scope hierarchy" + `web/apps/console/src/shared/ui/app-shell/scope-switcher.tsx` | `scope-switcher-popover.png`, `create-folder-dialog.png` (needs capture) | `<Step>`, `<Callout>` | Medium |
| 1.4 | Issue your first API key and call a resource | `/docs/getting-started/first-api-key` | Issue an API key for a service account and curl a `GET /api/v1/...` to prove the stack is wired. | `openspec/specs/identity/spec.md` §"API keys for service-to-service auth" | `api-keys-page.png` (needs capture), inline terminal transcript | `<Tabs>`, `<Callout type="warning">` | Medium |

### Chapter 02 — Concepts (8 pages)

| # | Title | Slug | Purpose | Source | Screenshots | Components | Length |
|---|-------|------|---------|--------|-------------|-----------|--------|
| 2.1 | Organizations, teams, and folders | `/docs/concepts/orgs-teams-folders` | The mental model for the 3-tier scope hierarchy. The single most important concept page — link here from every other page. | `openspec/specs/realm/spec.md` §"Organization / Team / Folder", existing `/docs/concepts/content.mdx` | A simple text diagram (build an SVG or an info card) | `<Callout>`, diagram (inline SVG) | Long |
| 2.2 | Authentication: local users and SSO | `/docs/concepts/auth-and-rbac` | How Plexor authenticates a request: local Sigil email+password vs external OIDC, what gets baked into the JWT, what the bearer format means. | `openspec/specs/identity/spec.md` + `openspec/specs/auth-providers/spec.md` | None — pure text + a JWT-claim table | — | Medium |
| 2.3 | Permissions and roles | `/docs/concepts/permissions` | What a permission is, how roles aggregate them, why there are no wildcards (`compute.vms.create.bulk` ≠ `compute.vms.create`). The flat-string catalog. | `openspec/specs/identity/spec.md` §"Flat permission strings with no wildcards" + §"RoleBinding" | None — link to **Ch 05 — permission catalog** | `<Callout type="warning">` | Medium |
| 2.4 | Compute workloads and runtimes | `/docs/concepts/compute-and-workloads` | What a workload is, what the runtimes are (docker-compose, podman-quadlet, k3s), what the lifecycle states mean. | `openspec/specs/clusters/spec.md` §"Workloads run on a runtime", §"WorkloadLifecycleState state machine" | `workloads-list.png`, `workload-state-flow-diagram.svg` (needs capture + design) | `<Callout>`, diagram | Long |
| 2.5 | Networking, floating IPs, and load balancers | `/docs/concepts/networking` | VPCs, subnets, security groups (planned), floating IPs, load balancers. What's the model, what can you attach. | `openspec/specs/network/spec.md` + `openspec/specs/clusters/spec.md` §"WireGuard mesh" | None in MVP — diagrams only | diagram (inline SVG) | Medium |
| 2.6 | Storage: volumes and buckets | `/docs/concepts/storage` | Block volumes vs S3 buckets. Sizing, attachment, when to use which. | `openspec/specs/storage/spec.md` | None — diagram | diagram | Medium |
| 2.7 | Quotas — why they exist and how they're resolved | `/docs/concepts/quotas` | The folder → team → org resolution walker, the 80% warning signal, the catalog keys that ship in v0.1. | `openspec/specs/quotas/spec.md` §"Effective value resolution", §"QuotaDefinition catalog" | None — text + 2 reference tables | `<Callout type="info">` | Medium |
| 2.8 | Audit log — what gets recorded | `/docs/concepts/audit` | Every state-changing call writes an `AuditEntry`. What the columns mean, how to query, retention window. | `openspec/specs/audit/spec.md` §"AuditEntry table", §"GET /api/v1/audit query endpoint" | `audit-page.png` (needs capture) | — | Medium |

### Chapter 03 — How-to (12 pages)

| # | Title | Slug | Purpose | Source | Screenshots | Components | Length |
|---|-------|------|---------|--------|-------------|-----------|--------|
| 3.1 | Create a workload (VM, container, pod) | `/docs/how-to/create-workload` | Step-by-step create of a workload through the console, equivalent YAML for the API. | `openspec/specs/clusters/spec.md` §"Start / Stop / Delete commands route through IComputeProvider" | `create-workload-dialog.png`, `workloads-list.png` (needs capture) | `<Step>`, `<Tabs>` | Long |
| 3.2 | Start, stop, and delete a workload | `/docs/how-to/manage-workload-lifecycle` | The five lifecycle states and the four buttons between them. Includes the "Pending → Failed — operator delete (cancelled)" path. | `openspec/specs/clusters/spec.md` §"WorkloadLifecycleState state machine" | `workload-actions-menu.png`, `workload-state-transition.png` (needs capture) | `<Tabs>` | Medium |
| 3.3 | Attach a volume to a workload | `/docs/how-to/attach-volume` | Provision a volume, attach it to a workload, expand it without downtime. | `openspec/specs/storage/spec.md` §"CreateVolumeAsync", §"UpdateVolumeSizeAsync" | `volume-create-dialog.png` (needs capture) | `<Step>`, `<Callout type="warning">` | Medium |
| 3.4 | Create a bucket and connect with an S3 client | `/docs/how-to/create-bucket` | DNS label rules, region label, the access-key copy flow. | `openspec/specs/storage/spec.md` §"Bucket entity" | `bucket-create-dialog.png` (needs capture) | `<Step>` | Medium |
| 3.5 | Reserve a floating IP and assign it to a node | `/docs/how-to/reserve-floating-ip` | Reserve, assign, reassign, release. | `openspec/specs/network/spec.md` §"FloatingIp" + §"ReassignFloatingIpAsync" | (text only in MVP — no UI to capture) | `<Step>` | Terse |
| 3.6 | Add a load balancer in front of two workloads | `/docs/how-to/add-load-balancer` | Round-robin vs least-conn; LB target list; what "Active" status means. | `openspec/specs/network/spec.md` §"LoadBalancer entity" + §"CreateLoadBalancerAsync" | (text only in MVP) | `<Callout>` | Medium |
| 3.7 | Add a user and assign them to a role | `/docs/how-to/add-user-and-role` | The console workflow + the equivalent API call (admin-only). | `openspec/specs/identity/spec.md` §"RoleBinding" | `users-list.png`, `create-user-dialog.png`, `role-binding-form.png` (needs capture) | `<Step>` | Medium |
| 3.8 | Issue an API key for a NodeAgent | `/docs/how-to/issue-api-key` | Subset-of-owner permission rule, copy-once flow, the `kid_<id>.<secret>` bearer shape. | `openspec/specs/identity/spec.md` §"API keys for service-to-service auth" | `api-key-create-dialog.png` (needs capture) | `<Callout type="warning">` | Medium |
| 3.9 | Connect an organisation to your external identity provider (OIDC) | `/docs/how-to/configure-oidc` | Per-tenant selection (one org = Sigil, another = OIDC), the discovery-document test endpoint, `*` admin retention. | `openspec/specs/auth-providers/spec.md` §"REST endpoints for org admin", §"Encryption-at-rest via IDataProtector" | (text only in MVP — page is admin IAM) | `<Callout type="warning">`, `<Step>` | Long |
| 3.10 | Customize your console's brand: name, logo, accent, theme | `/docs/how-to/customize-branding` | The BrandingConfig admin page, live preview, the `customCss` escape hatch and its 16 KiB cap. | `openspec/specs/branding/spec.md` §"AdminBrandingPage + LiveBrandPreview" | `admin-branding-page.png`, `live-brand-preview.png` (needs capture) | `<Step>` | Medium |
| 3.11 | Install and activate a community theme | `/docs/how-to/install-community-theme` | The marketplace UI, the activate/deactivate hook. Explicitly notes v0.1 ships two community themes; backend persistence lands later. | `openspec/specs/branding/spec.md` §"ThemeMarketplacePage admin UI" + `openspec/changes/theme-marketplace/proposal.md` | `theme-marketplace-page.png` (needs capture) | `<Step>` | Medium |
| 3.12 | Rotate a workload's SSH key | `/docs/how-to/rotate-ssh-key` | Add a new SSH key (fingerprint deduped), remove the old one, the `LastUsedAt` debounce (60s). | `openspec/specs/identity/spec.md` §"SshKey" | `ssh-keys-page.png`, `add-ssh-key-dialog.png` (needs capture) | `<Step>` | Terse |

### Chapter 04 — Admin guide (6 pages)

| # | Title | Slug | Purpose | Source | Screenshots | Components | Length |
|---|-------|------|---------|--------|-------------|-----------|--------|
| 4.1 | Reading and searching the audit log | `/docs/admin/audit-log` | The 4 query params, the `X-Quota-Warning: true` header rationale, retention window default (90 days, range 30–3650). | `openspec/specs/audit/spec.md` §"GET /api/v1/audit query endpoint" + §"Audit retention background service" | `audit-filter-form.png` (needs capture) | `<Callout>` | Long |
| 4.2 | Managing quotas — assignments, warnings, denials | `/docs/admin/quotas` | Where to set a folder override, what the 80% warning looks like in the UI, why there's no admin bypass. | `openspec/specs/quotas/spec.md` §"QuotaAssignment", §"Tiered enforcement" | `quotas-assignments-page.png`, `quota-warning-badge.png` (needs capture) | `<Callout>` | Long |
| 4.3 | Per-org RBAC hardening — minimum-permissions principles | `/docs/admin/rbac-hardening` | Why wildcards are dangerous, how to write a least-privilege role for a NodeAgent integration, why permission changes don't take effect on already-issued JWTs (15-min window). | `openspec/specs/identity/spec.md` §"Flat permission strings with no wildcards", §"RoleBinding" | None — pure guidance | `<Callout type="warning">` | Medium |
| 4.4 | Lockout recovery — failed-login counter and unlock | `/docs/admin/lockout-recovery` | The 5/10/15 thresholds, the 15-minute/1-hour/24-hour windows, manual DB unlock for break-glass. | `openspec/specs/identity/spec.md` §"Failed-login lockout" | (text + a state-diagram) | diagram | Medium |
| 4.5 | Backup, restore, and disaster avoidance | `/docs/admin/backup-and-disaster` | What Plexor backs up by design (control-plane state in Postgres, secrets in protected-data root), what it doesn't back up (workload disk contents). | `openspec/specs/identity/spec.md` §"Encryption-at-rest via IDataProtector" + `openspec/specs/clusters/spec.md` §"Cluster lifecycle starts with plx init" | (text + a checklist) | `<Callout type="warning">` | Long |
| 4.6 | Capacity planning and quota budgets | `/docs/admin/capacity-planning` | Default org-seeded quotas, when to raise the org vs adding a folder override, what to watch on dashboards. | `openspec/specs/quotas/spec.md` §"Default assignments seeded per org" | `quotas-usage-page.png` (needs capture) | `<Callout>` | Medium |

### Chapter 05 — Reference (3 pages)

| # | Title | Slug | Purpose | Source | Screenshots | Components | Length |
|---|-------|------|---------|--------|-------------|-----------|--------|
| 5.1 | REST API reference (auto-generated) | `/docs/reference/api` | The `/api/v1/...` surface, generated from `Program.cs` + `[EndpointName]` attributes. Initially a hand-curated list of endpoint groups by module; later auto-generated from OpenAPI. | All `openspec/specs/*/spec.md` REST-endpoint subsections | None — tabular content | table | Long (volatile) |
| 5.2 | Permission catalog | `/docs/reference/permissions` | Every `*.read`, `*.write`, `*.create`, `*.delete`, `*.assign.org`, etc. Hand-curated today, auto-generated tomorrow. | `openspec/specs/identity/spec.md` §"Flat permission strings" + scattered strings in every spec | None | table | Long |
| 5.3 | Glossary | `/docs/reference/glossary` | One-paragraph definitions for *workload*, *cluster*, *node*, *scope*, *FIP*, *quota assignment*, *quotum-assignment-effective-value*, *audit entry*, *theme installation*, *join token*, *bearer token*. | All specs | None — text-only | — | Medium |

### Chapter 06 — FAQ / Troubleshooting (2 pages)

| # | Title | Slug | Purpose | Source | Screenshots | Components | Length |
|---|-------|------|---------|--------|-------------|-----------|--------|
| 6.1 | Troubleshooting recipes | `/docs/troubleshooting` | Symptom → cause → fix for the top 10 things operators hit. Each item ≤30 lines; the page is one long scrollable list. Cross-references into Concepts and How-to. | Cross-cuts all specs | Inline console snippets (where applicable) | `<Accordion>` (one item per accordion, expanded by default) | Long |
| 6.2 | Frequently asked questions | `/docs/faq` | Short answers that fit one screen each. "Can two organizations share a workload?" — No, scope is exclusive. "Why is my quota warning not going away?" — Because raising the assignment doesn't reset the `CurrentValue`. | All specs | None | `<Accordion>` | Long |

### Roadmap (sidebar entries, no content yet)

These routes are in `nav-config.tsx` and `SECTIONS` but the spec is
not open for v0.1. They appear in the sidebar as **Soon** badges (the
existing pattern) and link to a single placeholder page each:

| Title | Slug |
|---|---|
| Managed PostgreSQL (Soon) | `/docs/roadmap/managed-postgres` |
| Managed Redis (Soon) | `/docs/roadmap/managed-redis` |
| Managed ClickHouse (Soon) | `/docs/roadmap/managed-clickhouse` |
| Managed Kafka (Soon) | `/docs/roadmap/managed-kafka` |
| Container Registry (Soon) | `/docs/roadmap/container-registry` |

A single `roadmap.mdx` chapter index lists these so future specs
land on top of already-mounted routes.

---

## 4. Screenshot inventory

**State today.** The repo has an empty
`repro-screenshots/` directory and no `web/apps/www/public/` yet.
Every page that names a screenshot in §3 needs **"needs capture"**
work — no fabricated PNG.

**Workflow.** One designated capture session, run from the local
console (per `agent-runtime-safety.md`, never from inside the
agent):

1. Boot `web/apps/console` against a running `Plexor.Host` (user
   runs `bun run dev:console` in their terminal — agent does not
   start the dev server).
2. Navigate to each page in the table below.
3. Use OS screenshot (`Win+Shift+S` / macOS `Cmd+Shift+4`) and
   save to `web/apps/www/public/screenshots/<chapter>/<page>/<n>.png`
   at a fixed viewport (1440×900) so the docs layout doesn't shift
   between captures.
4. Commit the screenshots alongside the page that uses them.

**Order matters.** Capture happens **after** the MDX page exists,
because the screenshot needs to be referenced from the page and the
import path must agree. The capture pass is therefore a *final*
step, not a parallel one.

**Capture list (deduped and ordered roughly by coverage-of-pages):**

| # | Screenshot | Used by | Notes |
|---|-----------|---------|-------|
| 1 | `login-screen.png` | 1.2 | Default paper theme; shows the brand applied pre-mount. |
| 2 | `console-shell-with-scope-switcher.png` | 1.2, 2.1 | Wide shell showing sidebar + scope switcher. The "hero" shot for the docs landing. |
| 3 | `scope-switcher-popover.png` | 1.3, 2.1 | The scope switcher popover showing org → team → folder tree. |
| 4 | `create-folder-dialog.png` | 1.3 | Inline-create dialog. |
| 5 | `api-keys-page.png` | 1.4, 3.8, 5.2 | Shows the list + the "issue" button. Captured once, reused. |
| 6 | `api-key-create-dialog.png` | 3.8 | The dialog showing the copy-once secret. |
| 7 | `workloads-list.png` | 2.4, 3.1, 3.2 | List of workloads across runtimes with state chips. |
| 8 | `create-workload-dialog.png` | 3.1 | Form fields; runtime picker. |
| 9 | `workload-state-transition.png` | 3.2 | One workload with the state chip in `Pending`. |
| 10 | `volume-create-dialog.png` | 3.3 | The create dialog with size picker. |
| 11 | `bucket-create-dialog.png` | 3.4 | The create dialog with region + name. |
| 12 | `users-list.png` | 3.7 | List of org users + their role bindings. |
| 13 | `create-user-dialog.png` | 3.7 | |
| 14 | `role-binding-form.png` | 3.7 | Per-folder binding. |
| 15 | `admin-branding-page.png` | 3.10 | The whole admin/branding page. |
| 16 | `live-brand-preview.png` | 3.10 | The form + the live preview side-by-side. |
| 17 | `theme-marketplace-page.png` | 3.11 | Grid of community themes with install/activate buttons. |
| 18 | `ssh-keys-page.png` | 3.12 | List of SSH keys + add button. |
| 19 | `add-ssh-key-dialog.png` | 3.12 | Form for paste-public-key. |
| 20 | `audit-page.png` | 2.8, 4.1 | Audit log list view. |
| 21 | `audit-filter-form.png` | 4.1 | The action-prefix + actor + date filters. |
| 22 | `quotas-assignments-page.png` | 4.2, 4.6 | Org-wide assignment table. |
| 23 | `quotas-usage-page.png` | 4.6 | Per-definition usage bars + 80% warning. |
| 24 | `quota-warning-badge.png` | 4.2 | The warning chip next to a resource. |

**Diagrams** (drawn in Figma/Excalidraw, exported as SVG, not as
PNG screenshots):

| # | Diagram | Used by |
|---|---------|--------|
| D1 | `org-team-folder-tree.svg` | 2.1 |
| D2 | `workload-state-machine.svg` | 2.4, 3.2 |
| D3 | `scope-resolution-walker.svg` | 2.7 |
| D4 | `network-topology.svg` | 2.5 |
| D5 | `lockout-thresholds.svg` | 4.4 |

Diagrams sit under `web/apps/www/public/diagrams/<n>.svg`.

---

## 5. Component reuse

### What already exists in `@plexor/ui` (or shared primitives)

Lifted from `web/packages/ui/src/*` and used by the console — each
is reusable inside MDX:

- `<Button>`, `<Input>`, `<Label>`, `<Popover>`, `<Dialog>`,
  `<DropdownMenu>`, `<AlertDialog>`, `<ScrollArea>` — primitives
  (`web/packages/ui/src/primitives/`). Useful in admin pages; not
  needed in user docs.
- `<Button variant=link>` style — for inline "see also" links.

The docs MDX does **not** depend on `@plexor/ui` by design. The doc
site is a separate deliverable and would otherwise couple to every
console refactor. Reuse is via **shared utility primitives** only
(Tailwind tokens via `@/lib/utils`).

### What exists in `web/apps/www/src/components/docs/` already

5 chrome components (`docs-sidebar.tsx`, `docs-header.tsx`,
`docs-toc.tsx`, `docs-footer.tsx`, `docs-breadcrumb.tsx`).
Reuse as-is; no edits needed for the new content.

### Gaps — new MDX-local components needed

The DOCS app needs 6 small, theme-aware MDX components. Each is a
single file in `web/apps/www/src/components/mdx/`:

| Component | Source of truth for shape | Notes |
|-----------|----------------------------|-------|
| `<Callout type="info|warning|tip|danger">` | shadcn-ui Radix-based pattern; props: `type` + `title?` + children | Borders + tinted background per type. ~30 lines. |
| `<Step number={n}>` | — | Renders a numbered step indicator next to a block of prose. Used in the how-to chapter. ~15 lines. |
| `<Screenshot src="..." alt="..." caption?>` | — | Wraps `<img>` with the right border + caption + lazy-loading. ~25 lines. |
| `<Tabs items={["Tab 1","Tab 2"]}>` / `<Tab>` | radix-ui `<Tabs>` already in `@plexor/ui` → reuse via `@/shared/ui/primitives/tabs` | Already exists; bring through the docs barrel. |
| `<Accordion>` (one per FAQ item) | radix-ui `<Accordion>` already in `@plexor/ui` | Same pattern. |
| `<CodeBlock lang="bash|json|toml">` | shiki via shadcn-ui wrapper | Already used in console; reuse. |

**Total new code:** ~150 lines of MDX components, all stub-level.
None need design — they're primitives the shadcn-ui ecosystem
ships.

### Diagrams

Diagrams are **inline SVG** authored in Markdown-style / SVG files
under `public/diagrams/<n>.svg`. Each diagram is hand-authored — no
runtime component. A `<Screenshot>` call referencing `.svg` works.

---

## 6. Capture workflow (described, not run)

**Per-page loop.** One MDX page at a time. Steps:

1. The agent authors the MDX page (next task — not this plan).
2. The MDX page references screenshots via `<Screenshot src="…">`.
3. The agent reads that list of `src` paths.
4. The agent asks the human (owner) to start `bun run dev:console`
   in their terminal against a running `Plexor.Host` instance with
   v0.1 features enabled.
5. The human navigates to the page(s) listed in §4 and saves a
   screenshot per `src` to
   `web/apps/www/public/screenshots/<chapter>/<page>/<n>.png`.
6. The agent verifies the file lands, then continues.

The agent **does not run `bun run dev`**, `vite`, `playwright`, or
any browser-launcher — `agent-runtime-safety.md` forbids dev/watch
processes and unconfined browser automation. Screenshots are a
**human-captured** artifact in this project.

For CI / reproducible baselines in the future, the project's
`bun run visual` story (per `~/.agents/rules/process/build-verification.md`)
will capture and diff-screenshot; that's **out of scope** for this
plan.

---

## 7. Effort estimate

Assuming the MDX components (§5) are built first as a small spike
(1 component per ~30 minutes, so ~half a day for all 6), then
authoring proceeds with one page at a time:

| Chunk | Pages | Estimate |
|---|---|---|
| Chapter 01 — Getting started | 4 | 1 day (includes updating the existing TSX) |
| Chapter 02 — Concepts | 8 | 2 days (longest prose, most diagrams) |
| Chapter 03 — How-to | 12 | 2 days (most cross-references, most captured screens) |
| Chapter 04 — Admin guide | 6 | 1.5 days |
| Chapter 05 — Reference | 3 | 1 day (mostly tables; auto-gen later) |
| Chapter 06 — FAQ / Troubleshooting | 2 | 0.5 day (reuses cross-references) |
| Roadmap placeholders (Soon pages) | 5 | 0.25 day (one shared template) |
| **Subtotal writing** | **40** | **~8 days of focused authoring** |
| MDX-component spike | — | 0.5 day |
| Screenshot capture + curation | ~24 PNGs + 5 SVGs | 1 day |
| Sidebar + chapter index wiring | — | 0.5 day |
| Final review pass + Edit-on-GitHub workflow | — | 1 day |
| **Total** | — | **~10–11 working days** (≈ 2 weeks of calendar with overflow for spec drift) |

Add ~30% padding for **spec drift**: the quotas spec was the last
one written (the audit module followed, then branding), and the
admin pages will land alongside code that's still moving. The plan
itself should be revisited when the next openspec change merges.

---

## 8. What NOT to do

These are non-starters that the plan **explicitly rules out**:

1. **No architecture diagrams in user docs.** Layers, modules,
   `forge`/`sigil`/`realm` schema names — all stay in `AGENTS.md`
   and `openspec/`. Operators see "what the system does for them",
   not "what it's built on".
2. **No install commands that don't exist yet.** The current console
   doesn't ship an install wizard; everything below "create a host"
   is a future-product promise that we will document when it ships.
3. **No screenshots of unimplemented console screens.** `nav-config.tsx`
   lists 23 routes. Only 9 of them have `to` defined. Pages like
   `/vms/new`, `/clusters`, `/admin/audit`, `/admin/theme-marketplace`
   ship in this plan; pages without `to` (Snapshots, SG, FIP, LB, DNS,
   Disks, Buckets, VolSnapshots, Users, Roles, SSH keys, API keys
   in admin) get "needs capture" — no faked PNG.
4. **No CLI commands.** The legacy `plx` CLI exists in
   `scope.md`/`architecture.md` but **not in v0.1 code**. The docs
   do not reference `plx init`, `plx provider install`, or any other
   CLI verb until the binary ships.
5. **No bundling of multiple responsibilities per page.** Each
   how-to page does **one** task; longer scenarios go in the
   admin guide or FAQ.
6. **No copy-paste from openspec specs verbatim.** Specs are RFC
   2119 (`SHALL`, `MUST`); docs are prose. Author retells.
7. **No links to AGENTS.md, openspec/, or .agents/docs/ from user
   docs.** Operators don't open those. Internal links point to
   *other pages in the docs site* only.
8. **No references to schema names.** `sigil`, `realm`, `atlas`,
   `forge`, `outpost`, `shard` are absent. `Plexor.Modules.*`,
   `ICurrentUser`, `IQuotaEnforcer`, `ThemeManifestVerifier` —
   absent.
9. **No fast-and-loose i18n.** The current landing is English-only;
   docs match. If the operator-facing UI ever ships RU/EN side by
   side (the brand pages already have RU/EN copy), docs follow.
   For now: English, no RU parity work in scope.
10. **No "Coming Soon" routes documented as live.** Routes in
    `nav-config.tsx` without `to` get a placeholder page saying
    exactly that.
11. **No mascot / marketing fluff in technical pages.** The landing
    page carries the brand voice; docs do not. Docs pages start
    with the operator's question, not "Plexor is great because…".

---

## 9. Appendix — what changes elsewhere

The plan author (this task) does **not** modify those, but the
follow-up task should:

- `web/apps/www/src/components/docs/docs-sidebar.tsx` — the five
  hard-coded chapter slots become the six real chapters in §2 (add
  `Reference` and `FAQ / Troubleshooting` slots, repurpose
  `Marketplace` and `Operations` into `Concepts`, `How-to`,
  `Admin guide`). Add the `soon` flag for the `Marketplace` slot
  if it stays.
- A new chapter-folder routing convention for
  `web/apps/www/src/routes/(docs)/docs/<chapter>/index.tsx` —
  the existing single-file pattern at
  `src/routes/(docs)/docs/getting-started.tsx` keeps working; we
  add nested folders per chapter.
- An MDX-tailwind prose style sheet at
  `web/apps/www/src/styles/prose.css` for `.docs-prose` (the class
  the existing TSX uses). MDX pages inherit the same class.
- The screenshot-and-diagram assets in
  `web/apps/www/public/{screenshots,diagrams}/` (created empty
  by this plan, populated by the capture workflow in §6).
- The 6 MDX components in
  `web/apps/www/src/components/mdx/` per §5.

---

*End of plan. The next task is to author the pages.*
