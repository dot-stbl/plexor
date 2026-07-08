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

/projects/{projectId}/marketplace         → NEW: app provider catalog
/projects/{projectId}/marketplace/{name}  → NEW: provider detail
/projects/{projectId}/marketplace/{name}/install → NEW: install form
/projects/{projectId}/marketplace/instances          → NEW: instance list
/projects/{projectId}/marketplace/instances/{id}    → NEW: instance detail

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
/admin/providers                       → Install providers (KVM/Ceph/OVS active)
/admin/marketplace/providers           → NEW: install/uninstall app providers
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
│  ⟶ Marketplace  │  NEW: app provider catalog
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
┌──────────────────────────────────────────────────────────────┐
│ [project ▾]    [🔍 search]              [🔔]   [👤 user ▾]    │
└──────────────────────────────────────────────────────────────┘
```

- **Project switcher** (left) — dropdown with all projects in current tenant
- **Global search** (center) — fuzzy search across VMs/volumes/etc.
- **Notifications** (right) — bell with unread count
- **User menu** (right) — profile / settings / sign out

## Routing conventions

| Pattern | Example | Notes |
|---------|---------|-------|
| `/projects/{pid}/{resource}` | `/projects/p-1/compute` | Standard resource listing |
| `/projects/{pid}/{resource}/{id}` | `/projects/p-1/compute/vm-1` | Resource detail |
| `/projects/{pid}/{resource}/new` | `/projects/p-1/compute/new` | Create wizard |
| `/projects/{pid}/{resource}/{id}/edit` | `/projects/p-1/compute/vm-1/edit` | Edit form |
| `/tenants/{tid}/{resource}` | `/tenants/t-1/projects` | Tenant-scoped resource |
| `/admin/{resource}` | `/admin/nodes` | Cross-tenant (admin only) |
| `/settings/{section}` | `/settings/profile` | Personal account |

All routes use **file-based routing** (TanStack Router).
404 → fallback to /tenants (or /login if not authenticated).

## Marketplace navigation

Marketplace — **главный новый раздел** под Marketplace sidebar item.
Имеет два подраздела:

```
/projects/{pid}/marketplace
├── /marketplace                → catalog (installed providers + browse)
/marketplace/{name}           → provider detail
│   └── /install              → install form
└── /instances                → running instances list
    └── /{id}                → instance detail
```

Sidebar order (важность для пользователя):
1. Compute (most used)
2. Storage
3. Network
4. **Marketplace** (NEW, 4th — после core infra, перед IAM)
5. IAM
6. Observability

Marketplace не отдельный top-level раздел — он внутри project scope
(т.к. app instances — per-tenant/per-project). Marketplace admin (`/admin/marketplace/providers`)
— отдельно для admin-level provider management (install/uninstall).

## Auth-gated routes

| Path | Required role |
|------|---------------|
| `/login` | public (anonymous) |
| `/tenants/*` | any authenticated user |
| `/admin/*` | `system-admin` role (cross-tenant access) |
| `/projects/{pid}/*` | member of project (any role) |
| `/billing/*` | tenant admin OR tenant owner |

**Default redirect** after login:
- If user has 1 project → `/projects/{firstProjectId}`
- If user has multiple → `/projects/{lastVisitedProjectId}` (or first)
- If user has no projects → show "no projects" empty state with "Request access" CTA

## Layouts (responsive)

### Desktop (>= 1024px)
- Sidebar visible (240px)
- TopBar visible
- Main content: 1 column, max-width none (fills)

### Tablet (768-1023px)
- Sidebar collapsed (icons only, 60px)
- TopBar visible
- Main content: 1 column, full width

### Mobile (< 768px) — NOT MVP
MVP — **desktop only**. Mobile responsive будет в Phase 2+.

## Empty routes (system-level)

| Route | When | UI |
|------|------|-----|
| `/tenants` | User has no tenant access | "No projects yet. Contact your admin." |
| `/projects/{pid}/compute` | No VMs | "No VMs. Create your first." + CTA button |
| `/projects/{pid}/marketplace` | No providers installed | "No providers. Browse catalog." + CTA |
| `/projects/{pid}/marketplace/instances` | No running instances | "No apps deployed. Browse marketplace." + CTA |

## What lives in `components/` (NOT screens)

`components/` folder содержит design briefs для переиспользуемых
компонентов (Button, Dialog, Table, etc.) — не для экранов.
См. [components/README.md](components/README.md).

Для **экранов** — используй [screens/](screens/) (layout-эскизы) +
[ui-inventory.md](ui-inventory.md) (поля/actions/states).

## См. также

- [ui-inventory.md](ui-inventory.md) — все экраны с полями/actions
- [ui-state-machines.md](ui-state-machines.md) — state transitions
- [user-flows.md](user-flows.md) — критичные сценарии
- [personas.md](personas.md) — типы пользователей
- [brand.md](brand.md) — Plexor DS токены
- [../architecture.md](../architecture.md) — общая архитектура
- [../modules.md](../modules.md) — модули и их endpoints
- [../providers.md](../providers.md) — provider model
