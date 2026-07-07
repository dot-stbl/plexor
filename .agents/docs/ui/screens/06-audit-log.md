# Screen 06: Observability → Audit Log

## Purpose

Timeline всех мутирующих операций в системе. Dmitriy ищет когда и кем
был изменён ресурс. Vasya проверяет что не было suspicious activity.
Andrey использует для compliance/инцидентов.

## User goal

Dmitriy: "Кто вчера в 03:42 выдал alice admin-роль?"

Andrey: "Покажи все операции за последний месяц, чтобы compliance-аудит."

Vasya: "Что вообще ломалось за последнюю неделю на этой VM?"

## Entry points

- Sidebar nav: Observability → Audit
- Direct URL: `/observability/audit`
- Quick link from notification alert

## Layout

```
┌──────────────────────────────────────────────────────────────────────┐
│ Top bar                                                              │
├──────────┬───────────────────────────────────────────────────────────┤
│ Sidebar  │ Observability > Audit                                      │
│          │                                                            │
│  Compute │ Time range: [Last 24h ▼]   Refresh: [5s ▼]            ⋯│
│  Storage │ Search: [search box ]   Export: [CSV] [JSON]             │
│  Network │                                                            │
│  IAM     │ Filters: actor ▾  action ▾  resource_type ▾  result ▾  │
│  Observ. │        [Apply filters] [Reset]                            │
│ ●Audit   │                                                            │
│  Metrics │                                                            │
│  Logs    │ ┌─ Timeline ─────────────────────────────────────────┐│
│          │ │ Timeline view (vertical) — see below                ││
│ ──────── │ │                                                       ││
│ Settings │ │ (one row per event, clickable)                       ││
│          │ │                                                       ││
│          │ │ Loading more...                                       ││
│          │ └───────────────────────────────────────────────────────┘│
└──────────┴────────────────────────────────────────────────────────────┘
```

## Content elements

### Filters (always visible, collapsible to chips)

- **Time range**: preset (15m, 1h, 6h, 24h, 7d, 30d, custom)
- **Actor**: dropdown of users (with email) or "system" or "API key"
- **Action**: dropdown with categories (compute.vms.*, network.*, etc.)
- **Resource type**: dropdown (vm, vpc, subnet, etc.)
- **Result**: succeeded / failed / partial
- **IP address**: text input (filter by source IP)
- **Text search**: full-text across payload

### Timeline (default view)

Each event row:
```
2026-07-07 12:33:42.123 UTC
  alice@acme.internal   (● user icon)
  ✓ compute.vms.reboot
    ├─ web-prod-1      (mono, clickable → VM detail)
    ├─ duration: 4.2s
    ├─ source IP: 198.51.100.4 (geo: US, NY)
    ├─ user-agent: Chrome/124.0
    └─ MFA: ✓ Yubikey
  [expand payload]
```

Click row → expands JSON payload inline:
```json
{
  "request": {
    "spec": {
      "name": "web-prod-1",
      "flavor": "small",
      ...
    }
  },
  "result": {
    "phase": "Running",
    "id": "vm-abc123"
  },
  "diff": {
    "before": { "flavor": "nano" },
    "after": { "flavor": "small" }
  }
}
```

**Row actions** (icon buttons):
- Copy event ID
- Show full payload (modal)
- Link to resource (target of the action)
- "Find related events" (same actor, same resource)

### Alternative views (toggle)

- **Timeline** (default) — vertical chronological
- **Table** — DataTable, sortable
- **By actor** — grouped
- **By resource** — grouped

## Event schema

```typescript
interface AuditEvent {
  id: string;                  // UUID
  timestamp: Date;             // UTC, microsecond precision
  actor: {
    type: 'user' | 'system' | 'api_key' | 'admin' | 'provider_plugin';
    id: string;                 // user id / api key id / 'system'
    email?: string;             // human-readable
    ip: string;                 // source IP
    geo: { country: string; city?: string };
    userAgent?: string;
    mfa?: { method: 'totp' | 'webauthn' | 'sms'; verified: boolean };
  };
  action: string;               // 'compute.vms.reboot'
  resource: {
    type: string;               // 'vm'
    id: string;
    displayName?: string;
  };
  tenant: { id: string; name: string };
  project?: { id: string; name: string };
  result: 'succeeded' | 'failed' | 'partial';
  errorCode?: string;
  errorMessage?: string;
  durationMs: number;
  payload: {
    request: object;            // what was asked
    diff?: { before: object; after: object };  // what changed
    result?: object;
  };
  correlationId?: string;       // for cross-service traces
}
```

## Cross-tenant audit (admin only)

- `/admin/audit` — same view but across ALL tenants
- Tenant column visible
- Same filters + tenant-specific filter

## Retention and storage

- Hot data: 90 days in PostgreSQL
- Cold archive: 365 days in S3 (Parquet format)
- Older: cold storage (Glacier-style)

UI shows what's available ("Showing last 90 days of detailed data"). Older data via `/admin/audit/export`.

## Permissions

- **Owner**: full audit log access
- **Admin**: tenant-scoped audit
- **Developer**: only own actions visible
- **System admin**: cross-tenant audit

## Performance

- Server-side pagination (cursor-based)
- Streaming via SSE for live updates ("tail mode")
- Indexed by (tenant_id, timestamp DESC)

## States

### Empty
- "No events match the filters" + reset button

### Loading
- Skeleton rows (5 placeholders, shimmer)

### Failed export
- Toast: "Export failed. Try smaller time range."

### Streaming mode
- "● Live" badge, scroll-pause on focus

## OpenDesign prompt

```
OpenDesign session for Plexor Portal > Audit Log

Critical for incident investigation and compliance.
Power-user screen — terminal-style preferences.

Required elements:
- Sticky filter bar (time range, actor, action, resource, result, IP, search)
- Export buttons (CSV, JSON)
- Timeline view (default) with expandable rows
- Each event: timestamp, actor with avatar, action, resource, badges (MFA, geo), payload expand

Variant views:
- Table view (DataTable)
- Grouped by actor
- Grouped by resource

Modal: full payload viewer (JSON syntax highlighted)

Live mode: streaming updates via SSE

Dark + Light.

File: audit-log.figma

Output: 5 frames (default, table, grouped-by-actor, payload modal, empty)
```

## Open design decisions

- [ ] Default view: Timeline vs Table
- [ ] Live mode: tail with pause, or opt-in?
- [ ] Payload: always visible (collapsible) or per-click modal?
- [ ] Geo info: prominent badge or inline?
- [ ] Filter persistence: per-user or localStorage?
- [ ] Related events: AI-suggested or manual?