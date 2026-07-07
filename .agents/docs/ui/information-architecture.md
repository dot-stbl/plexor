# Information Architecture

Sitemap, navigation, routing. Всё это описывает **что где находится**
и **как пользователь перемещается**.

## Sitemap (high-level)

```
/                                       → редирект на /projects/{currentProject}
/login                                  → OIDC flow
/tenants                                → tenant list (admin only)
/tenants/{tenantId}                     → tenant overview (admins + members)
/tenants/{tenantId}/projects            → projects list
/projects/{projectId}                   → project dashboard (default landing)

/projects/{projectId}/compute           → VM list
/projects/{projectId}/compute/new       → Create VM wizard (steps 1-6)
/projects/{projectId}/compute/{vmId}   → VM detail (tabs: Overview/Console/Metrics/Snapshots/Network/Storage/Security/Activity)
/projects/{projectId}/compute/images   → Custom images list
/projects/{projectId}/compute/snapshots → All snapshots

/projects/{projectId}/storage           → Tabs: Volumes / Buckets / Snapshots
/projects/{projectId}/storage/volumes/new
/projects/{projectId}/storage/buckets/new

/projects/{projectId}/network           → VPC list
/projects/{projectId}/network/{vpcId}  → VPC detail (tabs: Subnets / Floating IPs / LBs / SGs)
/projects/{projectId}/network/new      → Create VPC wizard

/projects/{projectId}/iam              → Tabs: Users / Roles / SSH keys / API keys
/projects/{projectId}/iam/users/{userId}

/billing                                → (tenant-level, admin only)
/billing/usage                          → Current usage
/billing/invoices                       → Invoice history
/billing/quota                          → Tenant quota management

/observability                          → (tenant-level, admin)
/observability/metrics                  → Prometheus-like view
/observability/logs                     → Log search
/observability/audit                    → Audit log timeline

/settings                               → User account settings
/settings/profile
/settings/password
/settings/tokens                       → Personal API tokens
/settings/appearance                   → Theme picker

/admin                                  → (system-admin only, cross-tenant)
/admin/nodes                           → Cluster nodes
/admin/providers                       → Provider plugins
/admin/audit                           → Cross-tenant audit
/admin/settings                        → Cluster settings
/admin/docs                            → API docs (Scalar)
```

## Navigation structure

### Primary sidebar (left, persistent)

```
┌─────────────────┐
│ ◐ plexor       │  brand mark
├─────────────────┤
│                 │
│  ⟶ Compute      │  icon + label
│  ⟶ Storage     │
│  ⟶ Network     │
│  ⟶ IAM         │
│  ⟶ Observability│
│                 │
│ ─── admin ─── │
│  ⟶ Billing      │  for admins/owners only
│                 │
│ ─── settings ─│
│  ⚙ Settings     │
└─────────────────┘
```

### Top bar (right)

```
┌─────────────────────────────────────────────────────────────────┐
│  Project: prod-cluster ▼    Region: us-east-1          🔍       │
│                                                       🔔 ◐ User │
└─────────────────────────────────────────────────────────────────┘
```

Elements:
- **Project switcher** (left) — переключает `/projects/{id}/*`
- **Region indicator** — non-clickable, just info (single-region MVP)
- **Search** (/) — global search for resources
- **Notifications** — bell with badge
- **User menu** — name + dropdown (Profile, Settings, Logout)

### Breadcrumbs

```
projects / prod-cluster / compute / web-prod-1
```

Each crumb clickable, except last.

## Routing rules

### Tenant scoping
- Everything under `/projects/{projectId}/*` is **scoped to that project**
- Cross-project operations (admin tools, billing, observability) at higher URLs

### Auth gates
- **Public**: `/login`, `/logout`, `/oidc/*`
- **Authenticated user**: `/projects/{id}/*` where user has access
- **Tenant admin**: `/tenants/*`, `/billing/*`
- **System admin**: `/admin/*`

Middleware checks per route group. Layouts:
```
/login                → LoginLayout (no sidebar)
/_tenant              → TenantLayout (sidebar, project switcher)
/_project             → ProjectLayout (sidebar, primary nav)
/_admin               → AdminLayout (sidebar, admin nav)
```

### URL parameters
- `{tenantId}` — required for tenant-scoped routes
- `{projectId}` — required for project-scoped routes
- `{resourceId}` — resource UUID
- `:slug` — for SEO-friendly URLs (e.g. `/vpcs/main` not `/vpcs/{uuid}`)

## Information density

### Compact vs spacious

Per persona:
- **Dmitriy**: compact, terminal-style, lots of info per row
- **Maria**: spacious, clear hierarchy, breathing room
- **Andrey**: KPI-focused, big numbers, focused screens
- **Vasya**: medium, technical detail visible

### Default: medium density
- Tables with 6-8 columns visible without horizontal scroll (≥1280px)
- Tabs when 4+ sections per resource
- Collapsible sidebars for power users

## Keyboard navigation

| Shortcut | Action |
|----------|--------|
| `/` | focus search |
| `g → c` | go to Compute |
| `g → s` | go to Storage |
| `g → n` | go to Network |
| `g → i` | go to IAM |
| `c` | create new (per-page) |
| `?` | help |
| `esc` | close modal / clear search |

Should be visible: `?` opens cheat sheet.

## Mobile / responsive

**Default assumption**: desktop-first (1024px+).
- Tablet (768-1024px): same layouts, slightly compressed
- Mobile (<768px): **read-only dashboards + viewing** (no edit flows)
  - Editing happens on desktop
  - Honest disclosure: "Best on desktop" badge on mobile views

## Permissions and visibility

Some actions only available to specific roles:
- `Owner` — full tenant control
- `Admin` — manage users + resources
- `Developer` — create/modify own resources
- `Viewer` — read-only

UI implications:
- Hide actions user can't perform (don't just disable)
- Show role badge next to user name
- Tooltip on hidden buttons: "Requires Admin role"

## Open design questions

- **Multi-project dashboard**: should we have a top-level `/projects`
  showing all projects user has access to, or always show one project
  at a time? Currently planned: always one project + switcher.
- **Multi-region**: currently info-only. When multi-region ships, add
  region switcher (top-bar).
- **Tabs vs pages**: VM detail has tabs (Overview/Console/Metrics/etc)
  or sub-routes (`/compute/{id}/console`)? Currently planned: tabs.
- **Search**: global or scoped? Currently planned: global + scoped
  (e.g. search VMs only).

Document decisions in `screens/0X.md` when you make them.