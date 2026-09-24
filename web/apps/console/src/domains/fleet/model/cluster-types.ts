/**
 * Cluster & Node types — self-hosted Plexor topology.
 *
 * A Cluster = one Plexor.Host control-plane + N Plexor.NodeAgent nodes
 * that joined via a join token. Users provision the control plane
 * themselves (ISO / `plx init`); the UI exposes join tokens and the
 * `plx node join` command so they can register additional nodes.
 *
 * NOT kubb-generated yet: cluster/node OpenAPI endpoints are part of a
 * later plan. Local types + local hooks + hand-curated mock data.
 *
 * Migration path: replace local types with `import type { ... } from
 * '@/shared/api'` once the kubb pipeline generates them.
 */

export type NodeStatus = 'pending' | 'ready' | 'draining' | 'offline';
export type NodeRole = 'control' | 'compute';
export type TokenStatus = 'active' | 'revoked' | 'expired';

export interface NodeSpec {
  vcpu: number;
  ramGb: number;
  /** Total block storage reachable from the node (Ceph pool, local LVM, etc.). */
  diskGb: number;
  /** Install providers selected for this node (kvm | lxd | pod, ovs | cilium, …). */
  providers: string[];
}

export interface PlexorNode {
  id: string;
  /** Self-reported hostname (Plexor.NodeAgent populates on join). */
  hostname: string;
  role: NodeRole;
  status: NodeStatus;
  spec: NodeSpec;
  /** ISO image the node was provisioned from. */
  isoVersion: string;
  /** When the node first joined the cluster. */
  joinedAt: string;
  /** Last heartbeat (heartbeat = every 30s from the agent). */
  lastSeenAt: string;
  /** Number of VMs currently scheduled on the node. */
  vmCount: number;
}

export interface JoinToken {
  id: string;
  /** Short human label set at issue time. */
  label: string;
  status: TokenStatus;
  /** Token value (opaque, 256 bits). Shown once on issue; revocable. */
  token: string;
  /** Restrict the token to a specific role (single-node cluster, control-plane node, …). */
  intendedRole: NodeRole;
  /** ISO versions the node must be on. */
  minIsoVersion: string;
  /** Issued at + expires at (TTL). */
  issuedAt: string;
  expiresAt: string;
  /** Which node redeemed the token (set after successful join). */
  redeemedByNodeId?: string;
}

export interface PlexorCluster {
  id: string;
  /** Cluster name as it appears in plx.yaml / 'plx init' output. */
  name: string;
  /** Which install providers were selected at plx init (e.g. ceph-rbd, ovs, kvm). */
  installProviders: string[];
  /** Version of the Plexor.Host binary running the control plane. */
  hostVersion: string;
  /** Uptime in seconds (host process start). */
  uptimeSeconds: number;
  /** Where the host is reachable (used in `plx node join <endpoint>`). */
  endpoint: string;
  /** Wall-clock the cluster was created (plx init). */
  createdAt: string;
  nodes: PlexorNode[];
  tokens: JoinToken[];
}

export interface NodeCounts {
  total: number;
  ready: number;
  pending: number;
  offline: number;
  draining: number;
}

export function countNodes(nodes: PlexorNode[]): NodeCounts {
  const c: NodeCounts = { total: 0, ready: 0, pending: 0, offline: 0, draining: 0 };
  for (const n of nodes) {
    c.total += 1;
    if (n.status === 'ready') c.ready += 1;
    else if (n.status === 'pending') c.pending += 1;
    else if (n.status === 'offline') c.offline += 1;
    else if (n.status === 'draining') c.draining += 1;
  }
  return c;
}

/** Human-readable uptime ("14d 2h", "3h 12m", "45s"). */
export function formatUptime(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    const remM = minutes % 60;
    return remM === 0 ? `${hours}h` : `${hours}h ${remM}m`;
  }
  const days = Math.floor(hours / 24);
  const remH = hours % 24;
  return remH === 0 ? `${days}d` : `${days}d ${remH}h`;
}