import type { K8sCluster } from './k8s-types';

const GIB = 1024 ** 3;

/**
 * Managed K3s clusters — UI mock. Fleet totals (vcpu/ramBytes) are summed across
 * the cluster's nodes; RAM in exact binary bytes so `Size` picks the unit.
 */
const CLUSTERS: K8sCluster[] = [
  {
    id: 'k8s-prod',
    name: 'prod-k3s',
    status: 'running',
    version: 'v1.31.1+k3s1',
    cpNodes: 3,
    workerNodes: 5,
    vcpu: 40,
    ramBytes: 96 * GIB,
    cni: 'Cilium',
    fleet: 'prod-cluster',
    endpoint: 'https://10.0.0.10:6443',
    createdAt: '2026-03-12T00:00:00Z',
  },
  {
    id: 'k8s-staging',
    name: 'staging-k3s',
    status: 'running',
    version: 'v1.30.5+k3s1',
    cpNodes: 1,
    workerNodes: 3,
    vcpu: 16,
    ramBytes: 32 * GIB,
    cni: 'Flannel',
    fleet: 'prod-cluster',
    endpoint: 'https://10.0.0.20:6443',
    createdAt: '2026-04-28T00:00:00Z',
  },
  {
    id: 'k8s-edge',
    name: 'edge-k3s',
    status: 'provisioning',
    version: 'v1.31.1+k3s1',
    cpNodes: 1,
    workerNodes: 2,
    vcpu: 8,
    ramBytes: 16 * GIB,
    cni: 'Flannel',
    fleet: 'edge-cluster',
    endpoint: 'https://10.1.0.10:6443',
    createdAt: '2026-07-08T09:15:00Z',
  },
];

export function listK8s(): K8sCluster[] {
  return CLUSTERS;
}
