import type { StatusVariant } from '@/shared/ui/primitives/status-pill';

/** Lifecycle state of a managed K3s cluster. */
export type K8sStatus = 'running' | 'provisioning' | 'degraded' | 'error';

/**
 * Managed Kubernetes (K3s) cluster. Self-hosted: `vcpu`/`ramBytes` are fleet
 * totals summed across the cluster's control-plane and worker nodes. `endpoint`
 * is the Kubernetes API server URL (copyable in the table).
 */
export interface K8sCluster {
  id: string;
  name: string;
  status: K8sStatus;
  version: string;
  /** Control-plane node count. */
  cpNodes: number;
  /** Worker node count. */
  workerNodes: number;
  /** Total vCPU across the cluster's nodes. */
  vcpu: number;
  /** Total RAM across the cluster's nodes, in bytes. */
  ramBytes: number;
  /** Container network interface (e.g. Cilium, Flannel). */
  cni: string;
  /** Node fleet the cluster is placed on. */
  fleet: string;
  /** Kubernetes API server endpoint. */
  endpoint: string;
  createdAt: string;
}

export function mapK8sStatusToVariant(status: K8sStatus): StatusVariant {
  switch (status) {
    case 'running':
      return 'ok';
    case 'provisioning':
      return 'info';
    case 'degraded':
      return 'warn';
    case 'error':
      return 'err';
  }
}
