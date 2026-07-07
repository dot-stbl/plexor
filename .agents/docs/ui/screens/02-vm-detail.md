# Screen 02: VM Detail

## Purpose

Полная информация о конкретной VM: status, configuration, console,
metrics, network, storage, audit trail. С этого экрана запускаются все
VM-операции.

## User goal

Dmitriy: when on-call — диагностировать проблему, восстановить работу.

Maria: проверить метрики, перезагрузить если зависла.

Vasya: посмотреть raw events + config.

## Entry points

- Click row in `/projects/{pid}/compute` (VM list)
- Direct URL: `/projects/{pid}/compute/{vmId}`
- Search results

## Layout

```
┌────────────────────────────────────────────────────────────────────────┐
│ Top bar                                                                │
├──────────┬─────────────────────────────────────────────────────────────┤
│ Sidebar  │ Breadcrumb: Compute / web-prod-1                            │
│          │                                                             │
│  Compute │ Header:                                                    │
│          │   ← back                                                   │
│          │   Title "web-prod-1" (large, monospace-style font)         │
│          │   StatusBadge "● Running (since 3d 2h)"                     │
│          │   External IP: 203.0.113.45 (mono, copy icon)              │
│          │   Flavor: small (2 vCPU, 4GB, 20GB SSD)                    │
│          │   [⋯ Actions menu]                                          │
│          │                                                             │
│          │ Tabs:                                                       │
│          │ [Overview] [Console] [Metrics] [Snapshots] [Network]        │
│          │ [Storage] [Security] [Activity log]                        │
│          │                                                             │
│          │ Tab content (depends on selected):                          │
│          │   see below                                                 │
└──────────┴─────────────────────────────────────────────────────────────┘
```

## Content elements per tab

### Tab: Overview (default)

```
┌─────────────────────────────────────────┬─────────────────────────────┐
│ Status                                  │ Recent events                 │
│ ● Running                                │ ● 2026-07-07 12:33  started  │
│ Phase: Running                          │ ● 2026-07-07 12:32  rebooted │
│ Last transition: 2026-07-04 09:33       │ ● 2026-07-04 09:33  started  │
│ Conditions:                             │ ● 2026-07-04 09:30  created  │
│  ✓ Ready                                │                               │
│  ✓ NetworkAttached                       │                               │
│  ✓ StorageReady                          │                               │
├─────────────────────────────────────────┼─────────────────────────────┤
│ Configuration                           │ Network                       │
│ Image: ubuntu-24.04                     │ VPC: main                     │
│ Flavor: small (2 vCPU, 4GB)             │ Subnet: app (10.0.1.0/24)     │
│ Created: 2026-07-04 12:30               │ Internal IP: 10.0.1.4         │
│ Created by: alice@acme                 │ Floating IP: 203.0.113.45     │
│ Zone: us-east-1a                        │ Security groups: web-tier     │
│ Node: node-1                            │                                │
├─────────────────────────────────────────┼─────────────────────────────┤
│ Labels                                  │ Storage                       │
│ env=prod  team=backend                  │ Volume: vol-web-1 (20GB)      │
│ cost-center=engineering                 │ Attached: ✓                   │
└─────────────────────────────────────────┴─────────────────────────────┘
```

### Tab: Console

```
┌───────────────────────────────────────────────────────────────────────┐
│ Toolbar: [⏻ Power actions] [ⓤ Upload file] [⌨ Send Ctrl+Alt+Del] [⛶ Fullscreen] │
├───────────────────────────────────────────────────────────────────────┤
│ xterm.js canvas:                                                          │
│  black background, monospace font, ANSI colors                          │
│                                                                       │
│  ubuntu@web-prod-1:~$ ls                                              │
│  bin   boot  dev   etc   home  lib   media  mnt   opt               │
│  proc  root  run   sbin  srv   sys   tmp   usr   var                │
│  ubuntu@web-prod-1:~$ █                                            │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘
│ Status: Connected • 60s heartbeat OK                                     │
```

When not connected: "Reconnecting..." with retry counter.

### Tab: Metrics

```
┌───────────────────────────────────────────────────────────────────────┐
│ Time range: [1h ▼]   Refresh: [5s ▼]    Graph type: [Line ▼]      │
├───────────────────────────────────────────────────────────────────────┤
│ CPU                                                                │
│  Line chart (5-min intervals)                                       │
│  [graph]                                                            │
│  Current: 23% • Avg: 18% • Peak: 67%                                │
├───────────────────────────────────────────────────────────────────────┤
│ Memory                                                             │
│  [graph]                                                            │
│  Used: 1.4GB / 4GB (35%)                                            │
├───────────────────────────────────────────────────────────────────────┤
│ Disk                                                               │
│  [graph]                                                            │
│  Read: 12 IOPS • Write: 47 IOPS                                    │
├───────────────────────────────────────────────────────────────────────┤
│ Network                                                            │
│  [graph in / out, separate lines]                                   │
│  In: 5.2 MB/s • Out: 12.4 MB/s                                     │
└───────────────────────────────────────────────────────────────────────┘
```

### Tab: Snapshots

Table similar to VM list, filtered to snapshots of this VM:

| Name | Created | Size | Status | Actions |
|------|---------|------|--------|---------|
| snap-001 | 2026-07-04 13:00 | 8.2 GB | Available | Restore · Delete |
| snap-002 | 2026-07-05 13:00 | 8.5 GB | Available | Restore · Delete |

"Create snapshot" button at top.

### Tab: Network

Shows:
- VPC + subnet (link to VPC detail)
- Internal IP, External IP (with floating IP)
- Security groups (chips linking to SG detail)

### Tab: Storage

List of attached volumes:

| Name | Size | Type | Attached | Actions |
|------|------|------|----------|---------|
| vol-web-1 | 20 GB | ssd | ✓ (sda) | Detach |

"+ Attach volume" → modal to select existing or create new.

### Tab: Security

- SSH keys currently injected (list with fingerprints)
- IAM service accounts (if any)
- Allowed actions: replace keys, revoke access

### Tab: Activity log

Filtered audit events for this VM:

```
┌───────────────────────────────────────────────────────────────────────┐
│ Filter: action ▾  actor ▾  time ▾                                     │
├───────────────────────────────────────────────────────────────────────┤
│ 2026-07-07 12:33:42  alice@acme  vm.reboot  →  succeeded              │
│ 2026-07-04 09:33:12  alice@acme  vm.create  →  succeeded              │
└───────────────────────────────────────────────────────────────────────┘
```

## Header actions (always visible)

`⋯` dropdown menu:
- Start (if Stopped)
- Stop (if Running)
- Reboot (if Running)
- Power cycle (force)
- Resize
- Create snapshot
- Edit labels
- **Delete** (red, with confirmation)

## States

### Loading
Skeleton of all tabs.

### Not found (404)
- Centered: "VM not found" + back button
- (Could be deleted between page loads — show toast if it was)

### Permission denied
- Read-only view: all tabs visible but no actions
- Banner at top: "You have read-only access"

### Pending / Provisioning
- Header shows spinner instead of status badge
- Tabs disabled except Overview
- Banner: "VM is being created... (this may take a few minutes)"

### Error
- Header status: red
- Alert banner: "VM is in error state. Last error: {message}"
- Quick action: "Open activity log" button

## Interactions

- Click IP (anywhere) → copy to clipboard + toast
- Click fingerprint → side panel with full key
- Click volume name → volume detail
- Click VPC name → VPC detail
- Tab change → query string update (browser-back works)

## OpenDesign prompt

```
OpenDesign session for Plexor Portal > VM Detail

Critical screen — Dmitriy will live here during incidents.

Required elements:
- Header with back arrow, title (mono font for VM name), status badge, IP+copy, flavor, ⋯ actions menu
- 8 tabs: Overview, Console, Metrics, Snapshots, Network, Storage, Security, Activity log
- Overview tab: 2-column grid of cards (Status, Configuration, Labels, Network, Storage, Events)
- Console tab: xterm.js dark canvas + toolbar (Power actions, Upload, Ctrl+Alt+Del, Fullscreen)
- Metrics tab: 4 line charts (CPU, Memory, Disk, Network) with time range selector
- Snapshots tab: DataTable + Create button
- Network/Storage/Security tabs: descriptive cards + linked chips
- Activity log tab: timeline with filters

Variants:
- Read-only mode (user without edit perm)
- Loading state (skeleton)
- Permission denied state
- Error state with banner

Dark mode + Light mode.

File: VM-detail.figma

Brand reference:
- Brand colors: #5E5BE8 (primary), #22D3EE (accent), state colors
- Typography: Inter (mono for IPs/IDs/keys)
- See /agents/docs/ui/brand.md

Output 5 variants:
1. Desktop 1440 Overview tab
2. Desktop 1440 Console tab
3. Desktop 1440 Metrics tab
4. Mobile 768 (read-only, simplified)
5. Empty/error state
```

## Open design decisions

- [ ] Default tab: Overview vs Console
- [ ] Metrics: graph rendering library (uPlot, Chart.js, Recharts?)
- [ ] Metrics retention in UI: 24h, 7d, 30d?
- [ ] Console: clipboard support? File upload to VM via console?
- [ ] Activity log: client-side filter or query-string?
- [ ] Power cycle: separate button or in ⋯ menu?
- [ ] VM delete confirmation: inline or modal? Require typing VM name?