/**
 * Networks fixtures — the network bounded context.
 *
 * Plexor self-hosted has no /api/v1/networks endpoint yet (the network
 * stack is still landing; see `.planning/`). This module exposes the
 * page shape we'll wire once the kubb contract lands: a list of VPCs
 * with their CIDR, subnet count, binding count and status. The empty
 * case (no VPCs) is the realistic first-run state — see `NetworksEmpty`.
 *
 * Each VPC carries a real `subnets` array — the per-subnet CIDRs are
 * carved out of the VPC's /16 so a later chunk can place VMs on actual
 * host addresses inside a real subnet (see `hostInSubnet`). `subnetCount`
 * is derived from `subnets.length` at construction time so it can never
 * drift from the breakdown.
 */

export type NetworkStatus = 'active' | 'draft';

export interface Subnet {
  id: string;
  vpcId: string;
  /** CIDR carved out of the parent VPC's range — must be a sub-range of the VPC cidr and must not overlap any sibling subnet. */
  cidr: string;
  /** Availability zone within the region, e.g. "eu-west-1a". */
  zone: string;
  /** Short role label shown nowhere yet but useful for later VM placement, e.g. "compute", "data", "edge". */
  purpose: string;
}

export interface Network {
  id: string;
  name: string;
  /** VPC CIDR (RFC1918 range). */
  cidr: string;
  region: string;
  /** Number of subnets inside the VPC — derived from `subnets.length`. */
  subnetCount: number;
  /** Number of resources bound to the VPC (VMs, LXC, k8s nodes, …). */
  bindingCount: number;
  status: NetworkStatus;
  createdAt: string;
  /** Subnets carved out of the VPC's CIDR. `subnetCount` is `subnets.length`. */
  subnets: Subnet[];
}

const prodSubnets: Subnet[] = [
  {
    id: 'subnet-prod-eu-compute-a',
    vpcId: 'vpc-prod-eu',
    cidr: '10.10.1.0/24',
    zone: 'eu-west-1a',
    purpose: 'compute',
  },
  {
    id: 'subnet-prod-eu-compute-b',
    vpcId: 'vpc-prod-eu',
    cidr: '10.10.2.0/24',
    zone: 'eu-west-1b',
    purpose: 'compute',
  },
  {
    id: 'subnet-prod-eu-data',
    vpcId: 'vpc-prod-eu',
    cidr: '10.10.3.0/24',
    zone: 'eu-west-1a',
    purpose: 'data',
  },
];

const stagingSubnets: Subnet[] = [
  {
    id: 'subnet-staging-eu-a',
    vpcId: 'vpc-staging-eu',
    cidr: '10.20.1.0/24',
    zone: 'eu-west-1a',
    purpose: 'compute',
  },
  {
    id: 'subnet-staging-eu-b',
    vpcId: 'vpc-staging-eu',
    cidr: '10.20.2.0/24',
    zone: 'eu-west-1b',
    purpose: 'compute',
  },
];

const devSubnets: Subnet[] = [
  {
    id: 'subnet-dev-eu-a',
    vpcId: 'vpc-dev-eu',
    cidr: '10.30.1.0/24',
    zone: 'eu-west-1a',
    purpose: 'compute',
  },
];

const NETWORKS: Network[] = [
  {
    id: 'vpc-prod-eu',
    name: 'prod-eu',
    cidr: '10.10.0.0/16',
    region: 'eu-west-1',
    subnetCount: prodSubnets.length,
    bindingCount: 14,
    status: 'active',
    createdAt: '2026-04-02T09:12:00Z',
    subnets: prodSubnets,
  },
  {
    id: 'vpc-staging-eu',
    name: 'staging-eu',
    cidr: '10.20.0.0/16',
    region: 'eu-west-1',
    subnetCount: stagingSubnets.length,
    bindingCount: 6,
    status: 'active',
    createdAt: '2026-05-18T14:44:00Z',
    subnets: stagingSubnets,
  },
  {
    id: 'vpc-dev-eu',
    name: 'dev-eu',
    cidr: '10.30.0.0/16',
    region: 'eu-west-1',
    subnetCount: devSubnets.length,
    bindingCount: 2,
    status: 'active',
    createdAt: '2026-06-30T11:05:00Z',
    subnets: devSubnets,
  },
];

export const SUBNET_BY_ID: ReadonlyMap<string, Subnet> = new Map(
  NETWORKS.flatMap((n) => n.subnets).map((s) => [s.id, s]),
);

/** All subnets across every VPC — a flat list is handy for VM placement. */
export function listSubnets(): Subnet[] {
  return NETWORKS.flatMap((n) => n.subnets);
}

/** Deterministic host address inside a subnet's /24 — offset must be 2..254.
 *  Used by the compute fixtures so every VM's internalIp is a real address
 *  inside a real subnet instead of an unrelated made-up range. */
export function hostInSubnet(subnetId: string, hostOctet: number): string {
  const subnet = SUBNET_BY_ID.get(subnetId);
  if (!subnet) throw new Error(`Unknown mock subnet id: ${subnetId}`);
  const base = subnet.cidr.split('/')[0]!.split('.').slice(0, 3).join('.');
  return `${base}.${hostOctet}`;
}

export function listNetworks(): Network[] {
  return NETWORKS;
}
