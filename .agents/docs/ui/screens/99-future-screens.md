# Future Screens (Phase 2+)

Экраны которые НЕ критичны для MVP, но должны быть в roadmap.
Описываем кратко — для дизайнера, чтобы при необходимости сделать.

## SSH Keys Management

**Where**: `/projects/{pid}/iam` → tab "SSH keys"

**Layout**: DataTable с колонками Name, Fingerprint, Added by,
Added on, Last used, Actions (Rename, Delete).

**Empty state**: "No SSH keys yet. Add your first key to access VMs."

**Add dialog**: paste public key, give it a name, optional download
private key pair (with warning about secure handling).

## Image Marketplace

**Where**: `/projects/{pid}/compute/images` (separate from "Custom images")

**Layout**: card grid, similar to image selector in VM wizard.

Categories:
- Public (upstream)
- Marketplace (curated by Plexor admins)
- Project (project's own private images)

## Database Management (Phase 2)

**Where**: `/projects/{pid}/databases`

Clusters:
- PostgreSQL
- Redis
- MongoDB (later)
- ClickHouse (later)
- Kafka (later)

For each:
- List view: cluster name, version, node count, storage, status
- Cluster detail: connection string, replication status, backups
- Create wizard: pick engine, version, nodes, storage, network

## Managed Kubernetes (Phase 2)

**Where**: `/projects/{pid}/compute/k8s-clusters`

Same as database + K8s-specific:
- Kubeconfig download
- Add-ons (ingress, monitoring)
- Node pools (groups of similar nodes)

## Container Registry (Phase 2)

**Where**: `/projects/{pid}/registry`

- Repositories list (with tags)
- Repository detail (tag list, vulnerabilities, push/pull stats)
- Webhook configuration
- Garbage collection policies

## Cloud DNS (Phase 3)

**Where**: `/projects/{pid}/network/dns`

- DNS zones list
- Zone detail (records table: name, type, value, TTL)
- DNSSEC toggle

## Quotas Management

**Where**: `/billing/quota` (tenant-level, admin)

- Per-project quota vs current usage
- Edit quota (with confirmation)
- Request increase (notify admin)

## Quota Approvals (admin only)

**Where**: `/admin/quota-requests`

Pending requests list with approve/reject buttons.

## Settings Hub

**Where**: `/settings`

Sub-pages:
- Profile (name, email, password, MFA)
- API keys (create, list, revoke)
- Appearance (theme picker, density)
- Notifications (email preferences)
- Connected accounts (GitHub, Google for OIDC providers)

## Admin Dashboard

**Where**: `/admin`

- Cluster nodes (health, capacity)
- Provider plugins (installed, available, install new)
- Cross-tenant resources
- System metrics
- Maintenance mode toggle
- Update management

## Object Storage Detail

**Where**: `/projects/{pid}/storage/buckets/{id}`

- Object browser (with path navigation)
- Upload (drag-and-drop, large file multipart)
- CORS / lifecycle / versioning tabs
- Access logs

## Security Group Detail

**Where**: `/projects/{pid}/network/{vpcId}/security-groups/{id}`

- Rules editor (visual builder)
- Source IP ranges with metadata
- Referenced by (which VMs)

## Floating IP Detail

- Reverse DNS editor
- Audit log filtered to this IP
- Attached/unattached history

## Load Balancer Detail

- Frontend config (port, TLS, certs)
- Backends list + health
- Connection draining settings
- Real-time traffic metrics

## Snapshot Detail

- Source VM/volume
- Size, creation time
- Restore button (with confirmation)
- Chain (parent snapshot if incremental)

## Bulk Operations

Multi-select on resources list → bulk actions:
- Start/Stop/Reboot/Delete VMs
- Delete volumes
- Release floating IPs
- Apply tag to many resources

## Resource Graph (visualization)

Network topology viewer for VPCs:
- Nodes = VPCs / Subnets / VMs
- Edges = network connections
- Interactive (click to drill in)

## Onboarding Wizard

First-time experience:
- Welcome screen
- Profile completion
- First project setup
- First resource (VM) creation
- Recommended next steps