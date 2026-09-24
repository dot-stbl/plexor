/**
 * Volume fixtures — the storage bounded context. No dedicated volumes
 * page exists yet; this establishes the VM -> volume relation inside
 * the mock "database" (one root volume per fleet VM, sized to its
 * `diskGb`) so audit/detail work later can reference a real volume id
 * instead of inventing one. Not wired into any MSW handler yet.
 */
import { FLEET } from '../compute/vms';

export type VolumeKind = 'root' | 'data';

export interface MockVolume {
  id: string;
  vmId: string;
  kind: VolumeKind;
  sizeGb: number;
  createdAt: string;
}

export const VOLUMES: ReadonlyArray<MockVolume> = FLEET.map((vm) => ({
  id: `vol-root-${vm.id}`,
  vmId: vm.id,
  kind: 'root' as const,
  sizeGb: vm.diskGb,
  createdAt: vm.createdAt,
}));

export function volumesForVm(vmId: string): MockVolume[] {
  return VOLUMES.filter((v) => v.vmId === vmId);
}
