# Screen 03: Create VM Wizard

## Purpose

Multi-step wizard для создания новой VM. Каждый шаг — одна логическая группа
настроек. На последнем шаге — review + submit.

## User goal

Maria: создать VM без понимания инфраструктуры, получить IP и быть
готовой к работе за 5 минут.

Vasya (advanced): пройти быстро с минимумом изменений defaults.

## Entry points

- `+ Create VM` button на VM list
- `/projects/{projectId}/compute/new`

## Wizard structure

6 шагов с прогрессом вверху и навигацией внизу:

```
┌──────────────────────────────────────────────────────────────────────┐
│ Top bar                                                              │
├──────────────────────────────────────────────────────────────────────┤
│ Breadcrumb: Compute / New VM                                         │
│                                                                      │
│ ┌─ Progress ──────────────────────────────────────────────────┐    │
│ │ ●━━━━━┯━━━━━┯━━━━━┯━━━━━┯━━━━━┯━━━━━┯━━━━━┯━━━━━┯━━━━━┯━━━━━○│   │
│ │ ①    ②    ③    ④    ⑤    ⑥                                 │    │
│ │ Basic Resrc Image Netwrk Accs  Review                            │    │
│ └──────────────────────────────────────────────────────────────┘    │
│                                                                      │
│                                                                  ▣   │
│ ── step content (see below) ──                                 │   │
│                                                                      │
│ Footer:                                                              │
│  [← Cancel / Previous]                              [Next ▶ / Create]│
└──────────────────────────────────────────────────────────────────────┘
```

## Step 1: Basics

### Purpose
Name, description, zone, labels.

### Fields

**Name** (text, required, validate: `[a-z0-9-]+`, 3-63 chars)
- Helper: "Used as hostname. Lowercase, alphanumeric, dashes"
- Auto-suggest: random suffix toggle

**Description** (text, optional, 0-200 chars)
- Helper: "Optional. Visible in VM list as tooltip"

**Zone** (radio cards, required)
- Cards: one per available zone in current region
- Show: zone name + count of available resources

**Labels** (key-value tags, optional)
- Add via "+ Add label"
- Each: key (lowercase), value (any)
- Validation: key matches `[a-z][a-z0-9-_]*`

### Validation
- Name unique within project → API check on blur
- Zone capacity pre-check (warn if low)

## Step 2: Resources

### Purpose
Choose flavor (или кастомный), persistent disk size.

### UI

**Flavor** — card grid:
```
┌─────────┬─────────┬─────────┬─────────┐
│ small   │ medium  │ large   │ custom  │
│ 2 vCPU  │ 4 vCPU  │ 8 vCPU  │  ⓘ      │
│ 4GB RAM │ 8GB RAM │ 16GB    │         │
│ 20GB    │ 40GB    │ 80GB    │         │
│ $0.01/h │ $0.04/h │ $0.16/h │         │
│ ($7/mo) │ ($29/mo)│ ($115/mo)│        │
│         │         │         │         │
│ ●●●     │ ○○      │ ○○      │ ○○      │
└─────────┴─────────┴─────────┴─────────┘
```
Selected card has filled radio + border.

**Custom flavor** (when "custom" selected):
- vCPU: slider 1-64
- RAM: slider 1-256 GB
- Disk: slider 20-1000 GB
- Cost calculation live

**Pricing note**: "Cost shown is for always-on. Stopped VMs don't accrue compute charges (storage still does)."

## Step 3: Image

### Purpose
Choose base OS image.

### UI
- **Tabs**: Public | Custom | Marketplace | Build (Phase 2)

**Public tab**:
- Search input
- Filter chips: family (Ubuntu, Debian, Alpine, RHEL, Windows), arch (x86_64, arm64)
- Card grid:
  ```
  ┌──────────────────────────┐
  │ [icon] ubuntu-24.04     │
  │ Ubuntu Server 24.04 LTS │
  │ x86_64 • 800MB • Latest │
  └──────────────────────────┘
  ```
- "Pull from URL" button — paste `docker://` or `https://` URL

**Custom tab**:
- List of project-private images
- "+ Upload custom image" button

**Marketplace tab**:
- Curated by Plexor admins
- Includes: Plexor PostgreSQL stack, Plexor Kubernetes node, etc.

### Selected indicator
- Filled radio in card corner
- Image details panel on right side (size, default user, when added)

## Step 4: Network

### Purpose
Choose VPC, subnet, security groups.

### UI
- **VPC selector**: dropdown (existing VPCs)
  - "Create new VPC" option → opens modal wizard
- **Subnet selector**: dropdown (filtered by selected VPC + zone from step 1)
  - Auto-pick first available if only one
- **Security groups**: multi-select chips
  - Default: none
  - "Create new SG" option
- **Floating IP**: checkbox "Allocate public IP" (default: on)
  - If on: "+$0.005/hour, removable"

## Step 5: Access

### Purpose
SSH keys, IAM service account, optional metadata.

### UI
- **SSH keys**: multi-select chips
  - "Add new SSH key" option → paste public key, give it a name
  - "Generate new key pair" option → shows private key once with download button
- **Service account**: dropdown of project service accounts (optional)
- **User data** (advanced, collapsed by default): textarea (cloud-init)
  - With preview if valid YAML / cloud-config
  - "Insert example" dropdown

## Step 6: Review

### Purpose
Final overview, cost estimate, submit.

### UI

```
┌──────────────────────────────────────────────────────────────────────┐
│ Summary                                                              │
│                                                                      │
│ Name: web-prod-1                                                     │
│ Zone: us-east-1a                Flavor: small (2 vCPU, 4GB, 20GB)   │
│ Image: ubuntu-24.04             Network: main/app, sg: web-tier      │
│                                                                       │
│ SSH keys: alice@laptop, alice@workstation                           │
│ Floating IP: yes                                                     │
│                                                                       │
│ Cost estimate                                                         │
│                                                                       │
│ Compute:  $0.01/hour     ($7.30/month if always-on)                  │
│ Storage:  included in flavor                                           │
│ Network:  $0.005/hour   ($3.65/month)                                │
│ ───────────                                                            │
│ Total:    $0.015/hour    ($10.95/month)                               │
│                                                                       │
│ ◉ I understand this will create a new VM and start billing.          │
│                                                                       │
│                                  [← Previous]    [Create VM ▶]        │
└──────────────────────────────────────────────────────────────────────┘
```

After click "Create VM":
- Show "Provisioning..." overlay
- Progress bar (estimated 30s)
- Streaming status updates via SSE
- Auto-navigate to VM detail page when Done

## States

### Validation error (any step)
- Inline red text under field
- "Next" button disabled

### Quota exceeded
- Show before submit: "This VM exceeds your project quota: 10 vCPUs requested, 5 vCPUs available. [Request quota increase]"

### Provider unavailable
- Show in step 1: "Some flavors may be temporarily unavailable in zone X"

### Provisioning failed
- Wizard stays, shows error banner, allows retry or back to edit

## Cancel

"Cancel" or browser-back:
- If anything dirty → confirm "Discard changes?"
- "Save as draft" option (saves form to localStorage, can resume later)

## OpenDesign prompt

```
OpenDesign session for Plexor Portal > Create VM Wizard

Critical screen — Maria's first experience with the product.
Must be friendly for non-technical users.

Required elements (6 steps):
1. Progress indicator at top (numbered circles + labels)
2. Step content area (large, breathing room)
3. Footer with Previous/Next buttons, Cancel on all steps

Step 1 - Basics: text inputs, radio zone cards, labels key-value
Step 2 - Resources: flavor cards grid + custom sliders
Step 3 - Image: search + filter chips + image card grid (with marketplace tab)
Step 4 - Network: VPC/subnet/SG selectors, floating IP toggle
Step 5 - Access: SSH key chips, user data textarea
Step 6 - Review: summary card + cost breakdown + confirm + submit

Variants:
- Empty (first time, no resources in project)
- Error (quota exceeded, validation failed)
- Provisioning in progress (modal overlay)

Dark mode + Light mode.

File: VM-create-wizard.figma

Brand: #5E5BE8 primary, $ cost per hour/month

Output: 6 frames (one per step) + 2 variants
```

## Open design decisions

- [ ] Number of steps: 6 (current) vs 3 (compressed with defaults)?
- [ ] Real-time cost calculation: include in review only, or every step?
- [ ] "Save as draft" feature for wizard state: yes/no, MVP?
- [ ] Marketplace images: separate tab, or mixed with Public?
- [ ] Custom flavor sliders vs numeric input?
- [ ] User data (cloud-init): advanced toggle or always visible?