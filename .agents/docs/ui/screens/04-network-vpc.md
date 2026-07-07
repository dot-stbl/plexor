# Screen 04: Network — VPC Detail

## Purpose

Управление одним VPC: subnet list, floating IPs, load balancers,
security groups в одной экранной области.

## User goal

Dmitriy: добавить subnet для нового use-case, настроить SG для нового
сервиса, привязать floating IP к новой VM.

## Entry points

- Click VPC name в VM detail (link)
- Sidebar nav "Network" → click VPC row
- Direct URL: `/projects/{pid}/network/{vpcId}`

## Layout

```
┌──────────────────────────────────────────────────────────────────────┐
│ Top bar                                                              │
├──────────┬───────────────────────────────────────────────────────────┤
│ Sidebar  │ Breadcrumb: Network / main-vpc                            │
│          │                                                            │
│  Compute │ ← back                                                     │
│  Storage │                                                            │
│  Network │ Title: main-vpc                                      ⋯   │
│  IAM     │ Subtitle: 10.0.0.0/16 • Region us-east-1                  │
│  Observ. │                                                            │
│          │ Tabs:                                                       │
│  Billing │ [Subnets] [Floating IPs] [Load balancers] [Security groups]│
│          │                                                            │
│          │ [Tab content]                                              │
│          │                                                            │
└──────────┴───────────────────────────────────────────────────────────┘
```

## Tabs

### Tab: Subnets (default)

DataTable:

| Name | CIDR | Zone | Available IPs | VMs | Actions |
|------|------|------|---------------|-----|---------|
| app | 10.0.1.0/24 | us-east-1a | 252 | 4 | Rename · Delete |
| db | 10.0.2.0/24 | us-east-1b | 254 | 2 | Rename · Delete |
| mgmt | 10.0.3.0/24 | us-east-1a | 254 | 1 | Rename · Delete |

"+ Create subnet" button (top right).

**Empty state**: "No subnets. Create first subnet to put VMs in this VPC."

### Tab: Floating IPs

DataTable:

| IP | Attached to | Reverse DNS | Status | Actions |
|----|------------|-------------|--------|---------|
| 203.0.113.45 | web-prod-1 (VM) | web.acme.internal | In use | Detach · Release |
| 203.0.113.46 | — | — | Reserved | Attach · Release |

"+ Allocate new IP" button.

### Tab: Load balancers

DataTable:

| Name | Type | Frontend port | Backends | Status | Actions |
|------|------|--------------|---------|--------|---------|
| web-lb | external | 443 (HTTPS) | 4 VMs (us-east-1a) | active | Configure · Delete |

"+ Create load balancer" button.

### Tab: Security groups

List of SGs (cards or table):

| Name | Rules | Attached to | Actions |
|------|-------|------------|---------|
| default-allow-ssh | 1 ingress / 0 egress | all VMs in this VPC | Edit · Delete |
| web-tier | 2 ingress / 1 egress | web-prod-1, web-prod-2 | Edit · Delete |

"+ Create security group" button.

## Header actions (always visible)

`⋯` dropdown:
- Rename VPC
- Add tag
- Export to JSON / YAML (IaC export)
- Delete VPC (with confirmation, requires: no attached resources)

## Sub-wizards / modals

### Create Subnet modal

```
┌──────────────────────────────────────────────┐
│ Create subnet                          [×]   │
├──────────────────────────────────────────────┤
│ Name *                                     │
│ ┌────────────────────────────────────┐      │
│ │ app                                │      │
│ └────────────────────────────────────┘      │
│ CIDR block * (must be inside VPC CIDR)    │
│ ┌────────────────────────────────────┐      │
│ │ 10.0.1.0/24                        │      │
│ └────────────────────────────────────┘      │
│ Zone *                                      │
│ ┌────────────────────────┐                │
│ │ us-east-1a     ▼      │                │
│ └────────────────────────┘                │
│ DHCP options                                │
│ ◉ enabled  ○ disabled                       │
│                                              │
│          [Cancel]      [Create subnet]     │
└──────────────────────────────────────────────┘
```

### Allocate Floating IP modal

- Select region (currently 1 — pre-selected)
- Optional: associate tag immediately
- "Allocate" button shows preview cost

### Create Load Balancer wizard (3 steps)

1. **Basics**: name, type (external/internal), protocol
2. **Frontend**: port (80/443), TLS cert selection
3. **Backends**: select VMs from VPC, health check config

## States

### Empty subnet list
- Banner: "Create your first subnet to add VMs to this VPC"

### No floating IPs
- Banner: "Allocate a floating IP to give a VM a public address"

### No security groups
- Banner: "Create at least one security group to control traffic"

### VPC delete confirmation (destructive)
- Show attached resources count
- Require typing VPC name to confirm
- Show list of resources that will be released

## Sidebar (VPC specific)

Within VPC detail, secondary sidebar showing all VPCs in current project — fast switcher:

```
┌────────────────────────┐
│ VPCs                   │
├────────────────────────┤
│ ◉ main                 │
│   10.0.0.0/16         │
│ ○ staging              │
│   172.16.0.0/16       │
│ ○ dev                  │
│   192.168.0.0/16      │
│ + Create VPC           │
└────────────────────────┘
```

## OpenDesign prompt

```
OpenDesign session for Plexor Portal > Network > VPC Detail

Dmitriy lives here when setting up new environments.

Required elements:
- Top header: title "main-vpc", subtitle "10.0.0.0/16 • region", ⋯ menu
- Secondary left sidebar: VPC list (project's VPCs) for fast switching
- Tabs: Subnets (default), Floating IPs, Load Balancers, Security Groups
- Each tab: DataTable + "Create" button + empty state

Modals to design:
- Create subnet (CIDR validation, zone select, DHCP toggle)
- Allocate floating IP
- Create load balancer wizard (3 steps)
- Create security group

Destructive: VPC delete with resource-check warning

Variants:
- Empty VPC (just created, no subnets)
- Full VPC (3+ subnets, IPs, LB, SGs)
- Read-only mode

Dark + Light.

File: network-vpc.figma

Output: 4 frames (one per tab) + 4 modals + 2 variants
```

## Open design decisions

- [ ] VPC switcher: secondary sidebar vs dropdown vs tabs?
- [ ] Subnet CIDR: free input or CIDR picker (calculator)?
- [ ] Floating IP reverse DNS: editable inline or modal?
- [ ] LB type variants: just ALB for MVP, or NLB too?
- [ ] Security group rules: visual builder or JSON?
- [ ] Network topology graph: separate page or button?