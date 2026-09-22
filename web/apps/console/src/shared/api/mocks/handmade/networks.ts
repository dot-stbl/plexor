// Handmade mock — networks page data for the self-hosted edition.
//
// Plexor self-hosted has no /api/v1/networks endpoint yet (the network
// stack is still landing; see `.planning/`). This module exposes the
// page shape we'll wire once the kubb contract lands: a list of VPCs
// with their CIDR, subnet count, binding count and status. The empty
// case (no VPCs) is the realistic first-run state — see `NetworksEmpty`.
//
// TODO(contract): replace this with kubb-generated handlers and delete
// this module. The page already goes through `useNetworks()` so the
// migration is one file.

export type NetworkStatus = 'active' | 'draft';

export interface Network {
  id: string;
  name: string;
  /** VPC CIDR (RFC1918 range). */
  cidr: string;
  region: string;
  /** Number of subnets inside the VPC. */
  subnetCount: number;
  /** Number of resources bound to the VPC (VMs, LXC, k8s nodes, …). */
  bindingCount: number;
  status: NetworkStatus;
  createdAt: string;
}

const NETWORKS: Network[] = [
  {
    id: 'vpc-prod-eu',
    name: 'prod-eu',
    cidr: '10.10.0.0/16',
    region: 'eu-west-1',
    subnetCount: 3,
    bindingCount: 14,
    status: 'active',
    createdAt: '2026-04-02T09:12:00Z',
  },
  {
    id: 'vpc-staging-eu',
    name: 'staging-eu',
    cidr: '10.20.0.0/16',
    region: 'eu-west-1',
    subnetCount: 2,
    bindingCount: 6,
    status: 'active',
    createdAt: '2026-05-18T14:44:00Z',
  },
  {
    id: 'vpc-dev-eu',
    name: 'dev-eu',
    cidr: '10.30.0.0/16',
    region: 'eu-west-1',
    subnetCount: 1,
    bindingCount: 2,
    status: 'active',
    createdAt: '2026-06-30T11:05:00Z',
  },
];

export function listNetworks(): Network[] {
  return NETWORKS;
}
