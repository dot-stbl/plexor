# UI Inventory — полный каталог экранов

Это **единая точка входа** для дизайнера. Каждый экран Plexor Portal —
с полями, actions, state transitions. Детальные layout-эскизы
(ASCII) — в `screens/0X-*.md`, state machines — в `ui-state-machines.md`.

**Версия:** соответствует новой provider model (install + app providers).
**Обновляй при:** добавлении нового экрана, изменении полей, изменении
state machine.

---

## Глобальный layout (все экраны)

```
┌──────────────────────────────────────────────────────────────────┐
│  TopBar  (project switcher · search · notifications · user)     │  56px
├──────────┬───────────────────────────────────────────────────────┤
│          │                                                       │
│          │                                                       │
│  Sidebar │  Page content                                        │
│  (left,  │                                                       │
│   240px) │                                                       │
│          │                                                       │
│          │                                                       │
└──────────┴───────────────────────────────────────────────────────┘
```

- **TopBar:** project switcher (left), global search (center),
  notifications + user menu (right)
- **Sidebar:** persistent nav, current section highlighted
- **Page content:** dynamic per route
- **Toaster (right bottom):** notifications (success/error/warn)
- **Modals:** centered, backdrop blur

---

## 1. Project context (always-on)

### 1.1 Project switcher (TopBar)

Показывает текущий project, при клике — dropdown со всеми projects
текущего tenant + кнопка "New project".

| Field | Type | Notes |
|---|---|---|
| Project name | text | Selected project display name |
| Project icon | icon | Optional emoji or 2-letter prefix |
| Tenant | text | Parent tenant name (smaller, secondary) |
| All projects | list | Searchable list of projects in current tenant |
| New project | button | Opens "Create project" modal |

**Actions:**
- Switch project → updates URL to `/projects/{newId}/*`, re-fetches all data
- Create project → modal with name + description
- Go to tenant → link to `/tenants/{tenantId}`

### 1.2 Notifications (TopBar)

| Field | Type | Notes |
|---|---|---|
| Unread count | number | Badge on bell icon |
| Recent | list | Last 10 notifications, sorted by time desc |
| Type | enum | info / success / warn / error |
| Title | text | Short description |
| Body | text | Long description (optional) |
| Resource link | link | Optional, points to related resource |
| Time | relative-time | "2 min ago" |

**Actions:**
- Click notification → navigates to resource OR marks as read
- Mark all as read → clears badge
- Filter by type → tabs (All / Unread / Errors)

### 1.3 User menu (TopBar)

| Field | Type | Notes |
|---|---|---|
| Avatar | image / initials | 32px, round |
| Name | text | User display name |
| Email | text | User email (small, muted) |

**Actions (dropdown):**
- Profile → `/settings/profile`
- API tokens → `/settings/tokens`
- Appearance → `/settings/appearance`
- Switch tenant (if user has access to multiple) → submenu
- Sign out → OIDC logout

---

## 2. Auth (pre-login)

### 2.1 Login (`/login`)

| Field | Type | Notes |
|---|---|---|
| Plexor logo | image | Centered, large |
| "Sign in to Plexor" | heading | |
| SSO button | button | "Continue with OIDC" (Keycloak / Authentik / etc.) |
| Email | input | For password fallback (local auth) |
| Password | input | For password fallback |
| Sign in | button | Disabled until fields valid |

**Actions:**
- SSO → redirects to OIDC provider (Keycloak), returns with JWT
- Local auth → calls `/api/v1/auth/login` directly
- Forgot password → external link

### 2.2 First-time setup (`/setup`)

Только при initial install, когда admin еще не создан.

| Field | Type | Notes |
|---|---|---|
| "Welcome to Plexor" | heading | |
| Tenant name | input | First tenant (usually "default") |
| Admin email | input | Becomes first user |
| Admin password | password | With strength meter |
| Region | select | eu-central-1 / us-east-1 / etc. |
| Create | button | Calls bootstrap endpoint |

---

## 3. Tenant / Project management

### 3.1 Tenant list (`/admin/tenants`)

| Field | Type | Notes |
|---|---|---|
| Name | text | Tenant display name |
| Slug | text | URL-safe identifier |
| Created | relative-time | When tenant was created |
| Projects | number | Count of projects in tenant |
| Users | number | Count of users in tenant |
| Quota used | progress-bar | CPU / RAM / Disk / Instances |
| Status | enum | active / suspended |

**Actions (per tenant):**
- View → `/admin/tenants/{id}`
- Edit name/slug
- Suspend / Reactivate
- Delete (only if no projects)

### 3.2 Tenant detail (`/admin/tenants/{id}`)

| Field | Type | Notes |
|---|---|---|
| Name | input | Editable |
| Slug | input | Editable, unique |
| Quota | form-group | CPU / RAM / Disk / max instances / max projects |
| Users | list | With role assignments |
| Projects | list | With quick-jump |
| Audit log | timeline | Recent actions in this tenant |

**Actions:**
- Save quota changes
- Invite user (email input → role select)
- Remove user from tenant
- Create project (modal)

### 3.3 Project list (within tenant)

Same pattern as tenant list, smaller scope.

### 3.4 Project detail

| Field | Type | Notes |
|---|---|---|
| Name | input | |
| Slug | input | |
| Description | textarea | Optional |
| Quota | form-group | Per-project limits |
| Default region | select | For multi-region setup |
| Members | list | Users with role on this project |

---

## 4. Compute (VMs, images, snapshots)

### 4.1 VM list (`/projects/{pid}/compute`)

| Field | Type | Notes |
|---|---|---|
| ☐ Checkbox | boolean | Row select for bulk actions |
| Name | link | → VM detail |
| Status | pill | running / stopped / provisioning / failed |
| Internal IP | mono | Plexor-internal address |
| External IP | mono | Public IP (if attached) |
| Flavor | text | small / medium / large / xlarge / custom |
| Node | text | Plexor.NodeAgent name |
| Created | relative-time | |
| Uptime | duration | "3d 2h" |

**Filters (top bar):**
- Status (multi-select)
- Zone / Node
- Flavor
- Labels (key=value)
- Search by name/IP

**Bulk actions (when ≥1 selected):**
- Start
- Stop
- Reboot
- Snapshot
- Delete
- Assign label

**Row actions (kebab menu):**
- View detail
- Start / Stop / Reboot
- Open console (noVNC)
- Create snapshot
- Resize (CPU/RAM)
- Migrate to another node
- Delete

### 4.2 VM detail (`/projects/{pid}/compute/{vmId}`)

| Field | Type | Notes |
|---|---|---|
| ← back | link | |
| Name | heading | + edit pencil |
| Status | pill | animated when transitioning |
| External IP | mono | copy button |
| Flavor | text | + resize action |
| Node | text | + migrate action |
| Created by | text | User email + timestamp |
| Uptime | duration | Live updating |

**Tabs:**
- **Overview** — basic info, recent events
- **Console** — noVNC terminal (iframe)
- **Metrics** — CPU/RAM/Network/Disk charts (last 1h/24h/7d)
- **Snapshots** — list of snapshots for this VM
- **Network** — attached VPCs, subnets, floating IPs, SGs
- **Storage** — attached volumes
- **Security** — SSH keys, security groups
- **Activity** — audit log for this VM

**Header actions:**
- ▶ Start / ■ Stop / ↻ Reboot (depending on state)
- Snapshots (dropdown)
- ⋯ More (Resize, Migrate, Delete)

### 4.3 Create VM wizard (`/projects/{pid}/compute/new`)

6-step wizard:

1. **Name & project** — name, optional description, labels
2. **Image (OS)** — image picker (catalog of public + private images)
3. **Flavor** — size selector (small/medium/large/xlarge or custom CPU+RAM)
4. **Storage** — root volume + additional volumes
5. **Network** — VPC + subnet + security groups + floating IP (yes/no)
6. **Review** — summary + Create button

Each step has ← Back / Next → / Cancel. Last step has "Create" instead of "Next".

### 4.4 Images (`/projects/{pid}/compute/images`)

| Field | Type | Notes |
|---|---|---|
| Name | text | Image name |
| Version | text | Tag/version |
| Source | enum | marketplace / custom-upload / snapshot |
| Size | bytes | Compressed size |
| OS | enum | linux / windows |
| Distribution | text | Ubuntu 22.04 / Debian 12 / etc. |
| Created | relative-time | |

**Actions:**
- Use in VM (dropdown to "Create VM with this image")
- Delete (if custom)

### 4.5 Snapshots (within VM detail or list)

| Field | Type | Notes |
|---|---|---|
| Name | text | |
| Size | bytes | Compressed |
| Created | relative-time | |
| Created by | text | User |

**Actions:**
- Restore (creates new VM from snapshot)
- Delete

---

## 5. Storage (volumes, buckets)

### 5.1 Storage list (`/projects/{pid}/storage`)

Tabs: **Volumes** | **Buckets** | **Snapshots**

### 5.1.1 Volumes tab

| Field | Type | Notes |
|---|---|---|
| Name | text | |
| Size | bytes | GB display |
| Type | enum | ssd / hdd / nvme |
| Attached to | link | VM name (or "detached") |
| Created | relative-time | |

**Actions:**
- Create volume
- Attach to VM (modal: pick VM)
- Detach
- Resize
- Snapshot
- Delete

### 5.1.2 Buckets tab

| Field | Type | Notes |
|---|---|---|
| Name | text | S3-compatible bucket name |
| Size | bytes | Total used |
| Objects | number | Object count |
| Endpoint | mono | S3 endpoint URL (copy) |
| Created | relative-time | |

**Actions:**
- Create bucket
- Browse objects (opens ObjectStore browser)
- Delete (with confirmation)

### 5.1.3 Snapshots tab

| Field | Type | Notes |
|---|---|---|
| Name | text | |
| Source | text | Volume name or VM name |
| Size | bytes | |
| Created | relative-time | |

**Actions:**
- Restore
- Delete

### 5.2 Create volume

| Field | Type | Notes |
|---|---|---|
| Name | input | |
| Size | number + unit | GB / TB |
| Type | radio | ssd / hdd / nvme |
| Attach to VM | optional | select existing VM or leave detached |

---

## 6. Network (VPCs, subnets, floating IPs, LBs)

### 6.1 VPC list (`/projects/{pid}/network`)

| Field | Type | Notes |
|---|---|---|
| Name | text | |
| CIDR | mono | 10.0.0.0/16 etc. |
| Subnets | number | Count of subnets in VPC |
| VMs | number | VMs in VPC |
| Created | relative-time | |

**Actions:**
- Create VPC (modal: name + CIDR)
- View detail → VPC detail page

### 6.2 VPC detail

| Field | Type | Notes |
|---|---|---|
| Name | heading | |
| CIDR | mono | |
| Subnets | list | Name + CIDR + VM count |
| Floating IPs | list | IP + attached VM |
| Load balancers | list | Name + backend VMs |
| Security groups | list | Name + rules count |

**Tabs:** Subnets | Floating IPs | Load Balancers | Security Groups

**Per-resource actions:** create, edit, delete (varies by type)

### 6.3 Create VPC

| Field | Type | Notes |
|---|---|---|
| Name | input | |
| CIDR | input | IP range, validated |
| Region | select | (if multi-region) |

### 6.4 Subnet (within VPC detail)

| Field | Type | Notes |
|---|---|---|
| Name | text | |
| CIDR | mono | Within VPC CIDR |
| Gateway | mono | First IP in range |
| VMs | number | Count |

**Actions:**
- Add subnet (modal)
- Edit CIDR
- Delete (only if empty)

### 6.5 Floating IP

| Field | Type | Notes |
|---|---|---|
| IP | mono | Public IP address |
| Attached to | link | VM name (or "available") |
| Region | text | Which region |

**Actions:**
- Allocate new (modal: pick region)
- Attach to VM (modal: pick VM)
- Detach
- Release

### 6.6 Load Balancer

| Field | Type | Notes |
|---|---|---|
| Name | text | |
| Public IP | mono | LB's public IP |
| Port | number | 80 / 443 / custom |
| Protocol | enum | HTTP / HTTPS / TCP |
| Backend VMs | list | VMs in the pool |
| Health check | text | Path / interval |

**Actions:**
- Add LB (modal: name + port + backend selection)
- Edit backends (add/remove VMs)
- Delete

### 6.7 Security Group

| Field | Type | Notes |
|---|---|---|
| Name | text | |
| Inbound rules | list | Port + protocol + source CIDR |
| Outbound rules | list | Port + protocol + dest CIDR |
| Attached VMs | number | Count |

**Actions:**
- Add rule
- Edit rule
- Delete rule
- Attach/detach VM

---

## 7. Marketplace (app providers) — NEW

Marketplace — главный новый раздел для новой provider model. Браузер
app providers + install/manage instances.

### 7.1 Marketplace catalog (`/projects/{pid}/marketplace`)

| Field | Type | Notes |
|---|---|---|
| Search | input | Filter by name/description |
| Category filter | select | cms / database / cache / web / identity / etc. |
| Tier filter | select | official / community / verified |
| Sort | select | name / popularity / recent |

| Provider | Type | Notes |
|---|---|---|
| Icon | image | Provider icon |
| Name | link | → provider detail |
| Display name | text | |
| Description | text | 1-line excerpt |
| Category | badge | |
| Tier | badge | official / community / verified |
| Version | text | Latest installed version |
| Status | enum | installed / not-installed |
| Install | button | If not installed → install source |
| Deploy | button | If installed → go to install-instance |

**Sections:**
- **Installed** (default tab) — providers installed in this cluster
- **Catalog** (browse official + community providers)

### 7.2 Provider detail (`/projects/{pid}/marketplace/{name}`)

| Field | Type | Notes |
|---|---|---|
| Icon + name + version | heading | |
| Description | text (long) | Multi-paragraph |
| Homepage | link (external) | |
| Category | badge | |
| Tier | badge | official / community |
| Maintainer | text + email | |

**Config schema section:**
For each parameter in provider.yaml's `config`:
| Field | Type | Required | Default | Validation | Description |
|---|---|---|---|---|---|
| name | string | yes | — | — | "Site title" |
| adminEmail | string | yes | — | email | "Admin email" |
| ... | ... | ... | ... | ... | ... |

**Resources required section:**
- CPU: 500m
- Memory: 512Mi
- Disk: 10Gi
- Ports: 80 (TCP, external)

**Dependencies:**
- postgresql >= 14.0 (auto-install)
- object-storage bucket (5Gi)

**Actions:**
- "Install instance" → goes to install form
- "View source" → opens provider.yaml / GitHub repo in new tab

### 7.3 Install instance (`/projects/{pid}/marketplace/{name}/install`)

Form with:
- Auto-generated fields from config schema (text inputs, dropdowns, etc.)
- "Show advanced" toggle → optional fields
- "Create" button → POST /marketplace/instances

If dependencies are not yet installed, show banner:
"PostgreSQL 15+ is required. Install now? [Yes / No]"

After submit:
- 202 Accepted with instance id
- Redirect to instance detail with status: installing

### 7.4 Provider instances list (`/projects/{pid}/marketplace/instances`)

| Field | Type | Notes |
|---|---|---|
| Name | link | → instance detail |
| Provider | text | wordpress / postgresql / etc. |
| Version | text | |
| Status | pill | installing / running / upgrading / failed / uninstalling |
| URL | link | Public URL if exposed |
| Created | relative-time | |
| Node | text | Which compute node |

**Filters:**
- Status
- Provider
- Search by name

**Actions (bulk):** Stop | Restart | Uninstall

**Row actions (kebab):**
- View detail
- Upgrade (dropdown to newer version)
- View logs
- Uninstall

### 7.5 Instance detail (`/projects/{pid}/marketplace/instances/{id}`)

| Field | Type | Notes |
|---|---|---|
| Name | heading | |
| Provider + version | text | "WordPress 0.2.0" |
| Status | pill | + animated when transitioning |
| URL | link | "http://203.0.113.42" (if exposed) |
| Node | text | |
| Internal IP | mono | (if applicable) |
| Created | relative-time | |
| Updated | relative-time | |

**Tabs:**
- **Overview** — config (as JSON), resources, health
- **Logs** — tail of install/upgrade logs
- **Events** — state transitions timeline

**Header actions:**
- Upgrade (opens version picker)
- Restart
- Uninstall

### 7.6 Provider install (admin) (`/admin/marketplace/providers`)

| Field | Type | Notes |
|---|---|---|
| Source | input | github.com/owner/repo, oci://..., ./local-path |
| Version | input | Optional, defaults to latest |

**Actions:**
- Install → fetches source, validates provider.yaml, stores in catalog
- List installed providers (admin view)
- Uninstall (only if no instances)

---

## 8. IAM (Identity & Access)

### 8.1 Users (`/projects/{pid}/iam/users`)

| Field | Type | Notes |
|---|---|---|
| Email | text | User identifier |
| Name | text | Display name |
| Roles | badges | All roles assigned to this user (across project) |
| Last login | relative-time | |
| Status | enum | active / disabled |

**Actions:**
- Invite user (modal: email + role)
- Edit roles (multi-select)
- Disable / Enable
- Remove from project

### 8.2 Roles (`/projects/{pid}/iam/roles`)

| Field | Type | Notes |
|---|---|---|
| Name | text | e.g. "developer", "admin" |
| Permissions | list | e.g. vm:read, vm:create, vm:delete |
| Users | number | Count of users with this role |

**Actions:**
- Create role (modal: name + permission multi-select)
- Edit permissions
- Delete (if no users)

### 8.3 SSH keys (`/projects/{pid}/iam/ssh-keys`)

| Field | Type | Notes |
|---|---|---|
| Name | text | |
| Fingerprint | mono | SSH key fingerprint |
| Public key | mono (truncated) | First 50 chars + "..." |
| Added | relative-time | |

**Actions:**
- Add key (textarea: paste public key)
- Delete

### 8.4 API tokens (`/projects/{pid}/iam/api-keys`)

| Field | Type | Notes |
|---|---|---|
| Name | text | |
| Token | mono (one-time shown) | Displayed once on creation, then hashed |
| Created | relative-time | |
| Last used | relative-time | |
| Expires | relative-time | Optional |

**Actions:**
- Create token (modal: name + expiry)
- Revoke (deletes)

---

## 9. Observability (admin only)

### 9.1 Audit log (`/admin/audit`)

Cross-tenant audit timeline.

| Field | Type | Notes |
|---|---|---|
| When | timestamp | Exact timestamp |
| Actor | text | User email (or "system") |
| Action | enum | vm.create / vm.delete / instance.install / etc. |
| Resource | text | Type + id (e.g. "vm/wp-7f3a2c") |
| Result | enum | success / failure |
| Metadata | json (collapsible) | Full request body / response |

**Filters:**
- Time range
- Actor (user)
- Action type
- Resource type
- Result (success/failure)
- Tenant

### 9.2 Metrics (`/admin/metrics`)

Prometheus-like view.

| Field | Type | Notes |
|---|---|---|
| Query | input | PromQL query |
| Time range | select | 1h / 6h / 24h / 7d / 30d / custom |
| Chart | area / line | Recharts visualization |
| Metrics | preset-buttons | CPU / memory / disk / network / app-instances |

**Saved queries:** user can save PromQL queries for reuse

### 9.3 Logs (`/admin/logs`)

| Field | Type | Notes |
|---|---|---|
| Query | input | Lucene-like or regex |
| Service | select | Filter by node-agent / host / etc. |
| Level | select | debug / info / warn / error |
| Time range | select | |
| Results | virtualized list | tail -1000 by default |

---

## 10. Billing (tenant admin)

### 10.1 Billing overview (`/billing`)

| Field | Type | Notes |
|---|---|---|
| Current month cost | large number | Sum of all usage |
| Forecast | number | Projected end-of-month cost |
| Breakdown | chart | By resource type (compute / storage / network) |
| By project | chart | Cost per project |

### 10.2 Usage (`/billing/usage`)

| Field | Type | Notes |
|---|---|---|
| Time range | select | Last 7d / 30d / 90d / custom |
| Resource type | select | All / compute / storage / network |
| Project | select | All / specific project |
| Chart | line | Usage over time (CPU-hours, GB-hours, GB egress) |
| Detail | table | Per-resource breakdown |

### 10.3 Invoices (`/billing/invoices`)

| Field | Type | Notes |
|---|---|---|
| Period | text | "April 2026" |
| Amount | currency | |
| Status | enum | draft / paid / overdue |
| Line items | list | Per-resource usage + cost |

**Actions:**
- View invoice (modal: line items + PDF download)
- Download PDF
- Mark as paid (admin only)

### 10.4 Quota (`/billing/quota`)

| Field | Type | Notes |
|---|---|---|
| Tenant quota | form-group | CPU / RAM / Disk / max instances / max projects |

---

## 11. Admin (cross-tenant, system admin only)

### 11.1 Cluster nodes (`/admin/nodes`)

| Field | Type | Notes |
|---|---|---|
| Name | text | Node hostname |
| Role | enum | control / compute |
| Status | pill | online / offline / degraded |
| CPU | progress-bar | Used / total |
| Memory | progress-bar | Used / total |
| Disk | progress-bar | Used / total |
| Plexor version | text | Plexor.NodeAgent version |
| Last heartbeat | relative-time | |

**Actions:**
- View detail (drain, restart services)
- Cordon (mark unschedulable)
- Drain (evacuate VMs)
- Remove from cluster

### 11.2 Cluster settings (`/admin/settings`)

| Field | Type | Notes |
|---|---|---|
| Cluster name | input | |
| Default region | input | |
| DNS suffix | input | |
| TLS cert | button | Re-issue / upload custom |
| Install providers | list | KVM / Ceph / OVS / etc. currently active |

### 11.3 API docs (`/admin/docs`)

Embedded Scalar OpenAPI explorer. Read-only documentation for all endpoints.

---

## 12. Settings (personal)

### 12.1 Profile (`/settings/profile`)

| Field | Type | Notes |
|---|---|---|
| Display name | input | |
| Email | text (read-only) | From OIDC |
| Avatar | file-upload | Optional |
| Time zone | select | |

### 12.2 Password (`/settings/password`)

For local-auth users only.

| Field | Type | Notes |
|---|---|---|
| Current | password | |
| New | password | With strength meter |
| Confirm | password | |

### 12.3 API tokens (`/settings/tokens`)

Personal access tokens (not project-scoped).

| Field | Type | Notes |
|---|---|---|
| Name | text | |
| Token | mono (one-time) | |
| Created | relative-time | |
| Last used | relative-time | |
| Expires | relative-time | |

**Actions:**
- Create
- Revoke

### 12.4 Appearance (`/settings/appearance`)

Theme picker:
- Light / Dark / System (auto)
- Accent color picker (planned, not MVP)

---

## 13. Empty / Loading / Error states

Каждый экран имеет три стандартных состояния. UI-компоненты в
`web/apps/console/src/shared/ui/primitives/`:

### 13.1 Empty state

- Иконка (large, muted)
- "No {resource} yet" (heading)
- "Get started by ..." (description)
- Primary action button → create flow

### 13.2 Loading state

- Skeleton placeholder matching actual layout shape
- No spinners for > 200ms loads (use skeleton)

### 13.3 Error state

- Error icon (large, err color)
- "Something went wrong" (heading)
- Error message (technical detail, collapsible)
- "Try again" button
- "Contact support" link

---

## 14. Cross-screen components

Эти компоненты переиспользуются на многих экранах:

| Component | Used in |
|---|---|
| `<TopBar>` | All pages (header) |
| `<Sidebar>` | All pages (left nav) |
| `<DataTable>` | All list screens |
| `<StatusPill>` | VM, Provider, Snapshot lists |
| `<EmptyState>` | All empty lists |
| `<Skeleton>` | All loading states |
| `<ErrorState>` | All error states |
| `<ConfirmDialog>` | All destructive actions |
| `<Toast>` | All async action results |

---

## 15. Permission matrix (summary)

| Role | View | Create | Edit | Delete | Admin |
|---|---|---|---|---|---|
| **Viewer** | ✓ | ✗ | ✗ | ✗ | ✗ |
| **Developer** | ✓ | ✓ | ✓ (own) | ✓ (own) | ✗ |
| **Admin** (project) | ✓ | ✓ | ✓ | ✓ | partial |
| **Owner** (tenant) | ✓ | ✓ | ✓ | ✓ | ✓ |
| **System admin** | ✓ (all tenants) | ✓ | ✓ | ✓ | ✓ |

Role assignment per resource type:
- VMs: developer can create/delete own
- Volumes: developer
- Buckets: developer
- VPCs/Subnets/SGs: admin only (infra)
- Users/Roles: owner only
- Marketplace instances: developer can create, admin can manage

---

## 16. New screens (Marketplace) — sitemap update

Add to existing `information-architecture.md`:

```
/projects/{projectId}/marketplace                    → provider catalog (NEW)
/projects/{projectId}/marketplace/{name}              → provider detail (NEW)
/projects/{projectId}/marketplace/{name}/install      → install form (NEW)
/projects/{projectId}/marketplace/instances          → instance list (NEW)
/projects/{projectId}/marketplace/instances/{id}    → instance detail (NEW)

/admin/marketplace/providers                         → provider admin (NEW)
```

Sidebar additions:
```
Marketplace    [providers icon]   for all users
```

---

## 17. Open design decisions

Что **не решено** и требует дизайнерского решения:

- **Theme color** — пока Plexor DS только монохромный + зелёный/красный для status. Нужен accent color picker?
- **Density** — compact vs default. List screens с 20+ строк — compact mode?
- **Mobile** — мы не делали mobile responsive. Confirm: desktop-only?
- **Onboarding flow** — first-time user, what they see, how they discover features
- **Provider author experience** — UI for authoring providers (not just install)
- **Multi-region** — UI for region switching if cluster spans regions

---

## См. также

- `screens/0X-*.md` — детальные layout-эскизы каждого экрана
- `ui-state-machines.md` — state transitions для ресурсов
- `user-flows.md` — критичные сценарии
- `information-architecture.md` — sitemap и navigation
- `personas.md` — типы пользователей
- `architecture.md` — UI tech stack
- `../architecture.md` — overall system architecture
- `../modules.md` — модули и их endpoints
- `../providers.md` — provider model (install + app)
- `../api-contracts.md` — HTTP API
