export interface ServiceDocLink {
  readonly label: string;
  readonly to: string;
}

export interface ServiceCapability {
  readonly title: string;
  readonly body: string;
  /** True for a capability that is designed/specified but not yet shipped — mirrors the page's own BentoStatus. */
  readonly planned?: boolean;
}

export interface ServiceScreenshot {
  readonly id: 'hero' | 'catalog' | 'audit';
  readonly caption: string;
}

export interface ServiceContent {
  /** 1-2 sentence hero lead, distinct from the shorter BENTO_CELLS body. */
  readonly lead: string;
  /** Where the hero's "Read the docs" CTA points — must be a real existing docs route. */
  readonly docsHref: string;
  readonly capabilities: readonly ServiceCapability[];
  readonly relatedDocs: readonly ServiceDocLink[];
  readonly screenshot?: ServiceScreenshot;
}

export const SERVICE_CONTENT: Readonly<Record<string, ServiceContent>> = {
  compute: {
    lead: 'Virtual machines, containers and Kubernetes pods, scheduled onto whichever runtime you chose at install — boot, resize and tear down from the same console you provisioned them in.',
    docsHref: '/docs/concepts/compute-and-workloads',
    capabilities: [
      { title: 'Three runtimes, one spec', body: 'Workloads run as docker-compose services, podman-quadlet pods, or k3s pods — pick one at install; the runtime is immutable after that.' },
      { title: 'A five-state lifecycle', body: 'Every workload moves Pending → Provisioning → Running → Stopped/Failed through one state machine, with start, stop, restart and delete as the only transitions.' },
      { title: 'Snapshots and console access', body: "Boot from an image, attach volumes and env vars, and reach a workload's console without leaving the browser." },
    ],
    relatedDocs: [
      { label: 'Workloads and runtimes', to: '/docs/concepts/compute-and-workloads' },
      { label: 'Create a workload', to: '/docs/how-to/create-workload' },
      { label: 'Workload lifecycle', to: '/docs/how-to/manage-workload-lifecycle' },
    ],
    screenshot: { id: 'hero', caption: 'VM list, sample data.' },
  },
  networking: {
    lead: 'Private networks, floating IPs and load balancers, wired together without touching a router.',
    docsHref: '/docs/concepts/networking',
    capabilities: [
      { title: 'VPCs and subnets', body: "Layer-2 isolated virtual networks with operator-chosen CIDR ranges; workloads on different VPCs can't reach each other without a peering link." },
      { title: 'Floating IPs', body: 'Reserve a public address, attach it to a workload, and re-attach it to another in seconds — no DNS propagation wait.' },
      { title: 'Load balancers', body: 'Round-robin or least-connections across a target list, with Active/Degraded/Inactive state computed every 30 seconds.' },
    ],
    relatedDocs: [
      { label: 'Networking concepts', to: '/docs/concepts/networking' },
      { label: 'Add a load balancer', to: '/docs/how-to/add-load-balancer' },
      { label: 'Reserve a floating IP', to: '/docs/how-to/reserve-floating-ip' },
    ],
  },
  storage: {
    lead: 'Block volumes and S3-compatible object buckets, on the disks already in the box.',
    docsHref: '/docs/concepts/storage',
    capabilities: [
      { title: 'Volumes — block storage', body: 'Size, attach and resize volumes online, backed by Ceph RBD or local-lvm; the guest OS owns partitioning and formatting.' },
      { title: 'Buckets — object storage', body: 'S3-compatible buckets over Ceph RGW or MinIO, private by default, with an explicit policy edit (and audit entry) to make one public.' },
      { title: 'Pick by access pattern', body: 'Random-access reads and writes go to a volume; large, append-mostly, shared data goes to a bucket.' },
    ],
    relatedDocs: [
      { label: 'Storage concepts', to: '/docs/concepts/storage' },
      { label: 'Attach a volume', to: '/docs/how-to/attach-volume' },
      { label: 'Create a bucket', to: '/docs/how-to/create-bucket' },
    ],
  },
  identity: {
    lead: 'Per-org users, roles and API keys — flat permission strings, no wildcards, bring your own IdP over OIDC.',
    docsHref: '/docs/concepts/auth-and-rbac',
    capabilities: [
      { title: 'Local users or OIDC', body: 'Email+password JWT logins by default, or hand the whole scheme to an external OIDC provider (Keycloak, Authentik, Azure AD) per organisation.' },
      { title: 'Flat permission strings', body: 'Every permission is `<service>.<resource>.<action>`, resolved as the union of every bound role — no wildcard expansion except the single `*` super-admin escape hatch.' },
      { title: 'Scoped role bindings', body: "Bind a role at org, team or folder scope; a folder-scoped editor can act in prod without ever seeing staging." },
      { title: 'API keys for services', body: "Long-lived service credentials whose permissions can never exceed their owner's — the mechanism that keeps a NodeAgent from escalating." },
    ],
    relatedDocs: [
      { label: 'Authentication and RBAC', to: '/docs/concepts/auth-and-rbac' },
      { label: 'Add a user and assign a role', to: '/docs/how-to/add-user-and-role' },
      { label: 'Issue an API key', to: '/docs/how-to/issue-api-key' },
      { label: 'Permissions catalog', to: '/docs/reference/permissions' },
    ],
  },
  'quotas-audit': {
    lead: 'Folder-level quotas and an append-only audit log on every state change — fully specified, not yet in a tagged release.',
    docsHref: '/docs/concepts/quotas',
    capabilities: [
      { title: 'Scope-hierarchy quota resolution', body: "Folder → team → org walk returns the tightest cap that applies, with the source scope recorded for the audit trail.", planned: true },
      { title: '80% warning, 100% block', body: 'A quota check inside the same transaction as the resource write — warn at 80%, deny with a stable error code at 100%, no silent overrides.', planned: true },
      { title: 'Append-only audit log', body: 'Every state-changing call writes an actor, action, target and payload, indexed for org- and user-scoped queries, retained 30-3650 days.', planned: true },
    ],
    relatedDocs: [
      { label: 'Quotas', to: '/docs/concepts/quotas' },
      { label: 'Audit log', to: '/docs/concepts/audit' },
      { label: 'Managing quotas', to: '/docs/admin/quotas' },
      { label: 'Reading the audit log', to: '/docs/admin/audit-log' },
    ],
    screenshot: { id: 'audit', caption: 'Audit log, sample data.' },
  },
  'app-catalog': {
    lead: 'Postgres, Redis, Keycloak and more, installed with one command from a versioned, signed manifest.',
    docsHref: '/docs/concepts',
    capabilities: [
      { title: 'One-command install', body: 'Provision a managed Postgres, Redis or Keycloak instance with a single command against the catalog.', planned: true },
      { title: 'Signed, versioned manifests', body: 'Every catalog entry is a signed manifest pinned to a version, so an install is reproducible and auditable.', planned: true },
      { title: 'Same lifecycle as compute', body: 'A catalog install becomes a workload like any other — start, stop and inspect it the same way.', planned: true },
    ],
    relatedDocs: [
      { label: 'Concepts overview', to: '/docs/concepts' },
    ],
    screenshot: { id: 'catalog', caption: 'Catalog, sample data.' },
  },
};

export function requireServiceContent(id: string): ServiceContent {
  const content = SERVICE_CONTENT[id];
  if (!content) {
    throw new Error(`No SERVICE_CONTENT entry for id "${id}".`);
  }
  return content;
}
