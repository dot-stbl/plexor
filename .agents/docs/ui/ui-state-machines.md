# UI State Machines — state transitions для ресурсов

Это **полный reference** всех state transitions в Plexor. Дизайнер
может использовать для правильного отображения UI (status badges,
кнопки, confirmations). Разработчик — для handlers и guards.

Формат: диаграмма состояний + таблица переходов.

---

## 1. Virtual Machine

```
                    ┌──────────┐
                    │ Requested│  ← POST /vms accepted, async provisioning
                    └────┬─────┘
                         │ scheduler picks node, creates spec
                         ▼
                 ┌────────────────┐
                 │ Provisioning   │  ← NodeAgent pulls image, starts libvirt
                 └────┬───────────┘
                      │
        ┌─────────────┼─────────────┐
        │ success     │ fail        │
        ▼             ▼             │
  ┌──────────┐   ┌──────────┐      │
  │ Running │   │  Failed  │      │
  └────┬────┘   └────┬─────┘      │
       │             │             │
       │ stop        │ retry       │
       │             │             │
       ▼             ▼             │
  ┌──────────┐   ┌────────────────┐
  │ Stopped │   │ Provisioning   │  ← retry transitions to Provisioning
  └────┬────┘   └────────────────┘
       │
       │ start
       ▼
  ┌──────────┐
  │ Running │
  └──────────┘

  Terminal: Deleting → (resource gone)
```

### VM state table

| From | Action | To | Notes |
|---|---|---|---|
| (none) | POST /vms | Requested | 202 Accepted, scheduler picks node async |
| Requested | scheduler success | Provisioning | spec row written, NATS event sent |
| Requested | scheduler fail | Failed | no node available, no quota, etc. |
| Provisioning | NodeAgent success | Running | libvirt started, IP assigned |
| Provisioning | NodeAgent fail | Failed | image pull error, libvirt error, timeout |
| Provisioning | user cancel | Deleting | — |
| Running | stop | Stopped | graceful shutdown |
| Running | reboot | Running | restart VM |
| Running | node fails | Failed | node lost, host down |
| Running | user delete | Deleting | confirm dialog |
| Stopped | start | Running | power on |
| Stopped | user delete | Deleting | confirm dialog |
| Failed | retry | Provisioning | attempt again |
| Failed | user delete | Deleting | — |
| Deleting | cleanup complete | (deleted) | resources freed, IP released |

### VM status display

| State | Badge color | UI elements |
|---|---|---|
| Requested | idle (gray) | spinner, "Creating..." |
| Provisioning | warn (yellow) | spinner, "Provisioning on node-XX" |
| Running | ok (green) | ●, "Running 3d 2h" |
| Stopped | idle (gray) | ○, "Stopped" |
| Failed | err (red) | ✕, "Failed: <reason>" |
| Deleting | warn (yellow) | spinner, "Deleting..." |

### VM actions by state

| State | Available actions |
|---|---|
| Requested | Cancel |
| Provisioning | Cancel (warning — may leave partial resources) |
| Running | Stop, Reboot, Snapshot, Resize, Migrate, Delete |
| Stopped | Start, Snapshot, Delete |
| Failed | Retry, Delete, View logs |

---

## 2. Volume

```
  (none) → Creating → Attaching → In Use → Detaching → Available
                              ↓
                          Deleting → (deleted)
```

| From | Action | To |
|---|---|---|
| (none) | POST /volumes | Creating |
| Creating | allocation success | Available |
| Creating | allocation fail | (deleted with error toast) |
| Available | attach to VM | Attaching |
| Attaching | attach success | In Use |
| Attaching | attach fail | Available (rollback) |
| In Use | detach from VM | Detaching |
| Detaching | detach success | Available |
| Available / In Use | user delete | Deleting |
| Deleting | cleanup complete | (deleted) |

### Volume display

- Available: green "● available" badge
- In Use: blue "● in use" badge, shows attached VM
- Creating/Deleting: yellow spinner

---

## 3. Snapshot

```
  (none) → Creating → Available ──delete──→ Deleting → (deleted)
                  │
                  ├──restore──→ new VM
                  │
                  └──schedule delete──→ Deleting
```

| State | UI |
|---|---|
| Creating | spinner, "Creating snapshot..." |
| Available | green checkmark, "Available • 1.2GB" |
| Deleting | spinner, "Deleting..." |

### Snapshot actions

- **Restore** — creates new VM from snapshot (creates a new VM with snapshot as image)
- **Delete** — destroys snapshot, releases storage

---

## 4. VPC

```
  (none) → Creating → Available ──delete──→ Deleting → (deleted)
                  │
                  ├──add subnet──→ (subnet Created → Available)
                  ├──add SG──→ (SG Created → Active)
                  └──attach VM──→ (VM joins VPC)
```

VPC has no state machine itself — it stays "Active" once created.
But VPCs have **resources** (subnets, SGs, floating IPs, LBs) that have their own state.

---

## 5. App provider instance (Marketplace)

```
  (none) → Resolving deps → Installing → Running ──upgrade──→ Upgrading → Running
                                              │              │
                                              │ fail         │ fail
                                              ▼              ▼
                                           Failed          Failed
                                              │
                                              │ uninstall
                                              ▼
                                          Uninstalling → (deleted)
```

| From | Action | To |
|---|---|---|
| (none) | POST /instances | Resolving |
| Resolving | deps installed (or already present) | Installing |
| Resolving | dep install fail | Failed |
| Installing | NodeAgent success | Running |
| Installing | NodeAgent fail | Failed |
| Running | upgrade | Upgrading |
| Running | restart | Installing (re-runs install hooks) |
| Running | uninstall | Uninstalling |
| Upgrading | success | Running (with new version) |
| Upgrading | fail | Running (rolled back to old version) |
| Failed | retry install | Installing |
| Failed | uninstall | Uninstalling |
| Uninstalling | success | (deleted) |

### App instance display

| State | Badge | UI elements |
|---|---|---|
| Resolving | idle | spinner, "Resolving dependencies..." |
| Installing | warn | spinner, "Installing on node-XX" |
| Running | ok | ●, "Running", link to URL |
| Upgrading | warn | spinner, "Upgrading to 0.3.0" |
| Failed | err | ✕, "Failed: <reason>" + View logs |
| Uninstalling | warn | spinner, "Uninstalling..." |

### App instance actions

| State | Actions |
|---|---|
| Resolving | Cancel |
| Installing | Cancel (warning) |
| Running | Upgrade (dropdown to versions), Restart, View URL, Uninstall |
| Upgrading | View logs (live) |
| Failed | Retry, View logs, Uninstall |
| Uninstalling | (none, cancel not supported) |

---

## 6. App provider (Marketplace catalog)

```
  (none) → Fetching → Validating → Available ──uninstall──→ Removing → (deleted)
                        │ fail
                        ▼
                      Invalid (with errors)
```

| State | UI |
|---|---|
| Fetching | spinner, "Fetching source..." |
| Validating | spinner, "Validating provider.yaml..." |
| Available | green checkmark, version, "Installed" |
| Invalid | err, "Invalid: <validation errors>" |
| Removing | spinner, "Removing..." |

---

## 7. Floating IP

```
  Allocating → Available ──attach to VM──→ Attached
                  │                       │
                  │ release              │ detach
                  ▼                       ▼
              (deleted)               Available
```

| State | UI |
|---|---|
| Allocating | spinner |
| Available | green dot, "Available" |
| Attached | blue dot, "Attached to vm-name" |

---

## 8. Load Balancer

```
  Creating → Active ──delete──→ Deleting → (deleted)
              │
              ├──add backend──→ (active, more backends)
              └──remove backend──→ (active, fewer backends)
```

| State | UI |
|---|---|
| Creating | spinner |
| Active | green, "Active • 3 backends" |
| Deleting | spinner |

Health status (per LB):
- Active (all backends healthy) — green
- Degraded (some backends unhealthy) — yellow
- Down (no backends healthy) — red

---

## 9. User

```
  Invited (email not confirmed) → Active
                                      │
                                      │ disable
                                      ▼
                                  Disabled
                                      │
                                      │ enable / re-invite
                                      ▼
                                  Active
```

| State | UI |
|---|---|
| Invited | yellow dot, "Invited" + resend button |
| Active | green dot, "Active" |
| Disabled | gray dot, "Disabled" + enable button |

---

## 10. Tenant

```
  Creating → Active ──suspend──→ Suspended ──reactivate──→ Active
                │                                       │
                │ delete (no projects)                 │ delete
                ▼                                       ▼
             Deleting                                 Deleting → (deleted)
```

| State | UI |
|---|---|
| Creating | spinner |
| Active | green dot |
| Suspended | yellow dot, "Suspended" + reactivate button |
| Deleting | spinner |

Suspended tenants: all API calls return 403. Resources not deleted, just frozen.

---

## 11. Project

Same lifecycle as Tenant.

```
  Creating → Active ──archive──→ Archived → reactivate → Active
                │
                │ delete
                ▼
             Deleting → (deleted)
```

---

## 12. Audit event

Audit events are **immutable** — no state machine. Once written, never change.
Each event has:
- timestamp (when)
- actor (who)
- action (what — verb.object)
- resource (target)
- result (success / failure)
- metadata (request body / response)

Read-only, sortable, filterable.

---

## Cross-resource state relationships

Some actions span multiple resources:

| Action | Affects |
|---|---|
| Delete VM | VM: Deleting, Volume: Detaching → Deleting, FloatingIP: Detaching → Available |
| Upgrade App instance | AppInstance: Upgrading → Running (new version), Dep volumes may be re-provisioned |
| Delete Tenant | All Projects → Deleting, All VMs in projects → Deleting, etc. (cascade) |
| Node drain | All VMs on node: live-migrated to other nodes (if possible) or stopped |

UI shows these cascades with:
- Toast: "VM deleted. Volume wp-data detached and deleted."
- Activity log: full chain of events

---

## Implementation mapping

Each state machine is implemented in:
- **Domain**: `enum ProviderStatus` etc. (sealed enum, no magic strings)
- **Application**: `IStateMachine<T>` interface with `CanTransition(from, to, action)` guard
- **Infrastructure**: persists current state in `<resource>_status` table + emits NATS event on transition

See [../modules.md](../modules.md) for module structure and
[../architecture.md](../architecture.md) for state management architecture.
