import type { CreateVmRequest } from '@/shared/api';

const GIB = 1024 ** 3;

/**
 * Placeholder VPC -> (vpcId, subnetId) lookup. The wizard's network
 * FieldRow ("VPC / subnet") today offers two placeholder VPC names
 * ('prod-vpc' | 'staging-vpc') with no subnet picker of its own (see
 * routes/vms/new.tsx's NETWORKS const) -- the create-VM contract needs
 * both a vpcId and a required subnetId. This table anchors each
 * placeholder onto a real subnet from the network domain's mock
 * fixtures (mocks/network/networks.ts) so the mock backend can place
 * the VM on a real subnet/zone/CIDR instead of falling back to a
 * synthetic default. Replace with a real network-domain lookup once
 * the wizard's network selector is wired to `@/domains/network`.
 */
const VPC_NETWORK_BY_SELECTION: Readonly<Record<string, { vpcId: string; subnetId: string }>> = {
  'prod-vpc': { vpcId: 'vpc-prod-eu', subnetId: 'subnet-prod-eu-compute-a' },
  'staging-vpc': { vpcId: 'vpc-staging-eu', subnetId: 'subnet-staging-eu-a' },
};
const FALLBACK_NETWORK = VPC_NETWORK_BY_SELECTION['prod-vpc']!;

/** Wizard fields that feed the create-VM contract request. Everything
 *  else the wizard collects today (placement, hypervisor, storage
 *  advanced knobs, cloud-init, NIC/VLAN/firewall, labels beyond tags,
 *  ...) has no home in `CreateVmRequest` yet -- this mapper only carries
 *  what the contract can currently express. */
export interface VmWizardValues {
  name: string;
  imageId: string;
  vpc: string;
  sockets: number;
  cores: number;
  ramBytes: number;
  bootDiskBytes: number;
  labels: readonly { key: string; value: string }[];
}

/** `key=value` tags from the wizard's label rows -- blank rows (both
 *  key and value empty) are dropped; a value-less key is kept bare. */
function labelsToTags(labels: readonly { key: string; value: string }[]): string[] | undefined {
  const tags = labels
    .map((l) => ({ key: l.key.trim(), value: l.value.trim() }))
    .filter((l) => l.key !== '' || l.value !== '')
    .map((l) => (l.value !== '' ? `${l.key}=${l.value}` : l.key));
  return tags.length > 0 ? tags : undefined;
}

/** Maps the create-VM wizard's local form state to the contract's
 *  `CreateVmRequest`. Pure -- no fetch, no JSX. `project` is always
 *  `'default'`: the wizard has no project/folder selector yet and
 *  every mock VM lives in the same default project. */
export function mapVmWizardToCreateVmRequest(values: VmWizardValues): CreateVmRequest {
  const vcpu = values.sockets * values.cores;
  const ramGb = Math.round(values.ramBytes / GIB);
  const network = VPC_NETWORK_BY_SELECTION[values.vpc] ?? FALLBACK_NETWORK;

  return {
    name: values.name.trim(),
    project: 'default',
    vpcId: network.vpcId,
    subnetId: network.subnetId,
    machineType: `${vcpu}-${ramGb}`,
    vcpu,
    ramGb,
    diskGb: Math.round(values.bootDiskBytes / GIB),
    image: values.imageId,
    tags: labelsToTags(values.labels),
  };
}
