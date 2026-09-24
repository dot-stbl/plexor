/**
 * In-memory VM store — the mutable half of the mock "database". Seeded
 * once from `compute/vms.ts` (`FLEET` + `FLEET_PLACEMENT`); mutations
 * (create/start/stop/delete) change this Map so a later read reflects
 * them, the way a real API would. Plain Map + functions — no classes,
 * no repositories. Every mutation appends an audit entry.
 */
import type { CreateVmRequest, Vm, VmStatus } from '@/shared/api';
import { FLEET, FLEET_PLACEMENT } from '../compute/vms';
import { SUBNET_BY_ID } from '../network/networks';
import { appendAuditEntry } from '../audit/audit';

export interface VmPlacement {
  nodeId: string;
  subnetId: string;
  vpcId: string;
  imageId: string;
}

interface VmRecord {
  vm: Vm;
  placement: VmPlacement;
  /** Wall-clock ms — once elapsed, a `provisioning` VM's effective
   *  status flips to `running` on the next read. */
  provisioningUntil?: number;
}

/** How long a freshly created VM stays `provisioning` before it's
 *  observed as `running` — a few seconds, so the UI can show the
 *  transition on a manual refresh/poll. */
const PROVISION_MS = 6000;

function seedRecords(): Map<string, VmRecord> {
  const records = new Map<string, VmRecord>();
  for (const vm of FLEET) {
    const placement = FLEET_PLACEMENT.get(vm.id);
    if (!placement) continue;
    records.set(vm.id, { vm: { ...vm }, placement });
  }
  return records;
}

let records = seedRecords();
let nextSeq = 1;

/** Re-seed from the fixtures — for tests that need isolation from
 *  mutations another test made. */
export function resetStore(): void {
  records = seedRecords();
  nextSeq = 1;
}

function resolve(record: VmRecord): Vm {
  if (record.vm.status === 'provisioning' && record.provisioningUntil !== undefined) {
    if (Date.now() >= record.provisioningUntil) {
      record.vm = { ...record.vm, status: 'running' as VmStatus };
      record.provisioningUntil = undefined;
    }
  }
  return record.vm;
}

export function listVms(): Vm[] {
  return [...records.values()].map(resolve);
}

export function getVm(id: string): Vm | undefined {
  const record = records.get(id);
  return record ? resolve(record) : undefined;
}

export function getVmPlacement(id: string): VmPlacement | undefined {
  return records.get(id)?.placement;
}

/** The `VmDetail` fields a `Vm` doesn't carry, derived from the
 *  record's placement — used when the handler answers `GET /vms/:id`,
 *  `POST /vms`, `/start`, `/stop`. */
export function getVmDetailExtras(id: string):
  | { project: string; vpcId: string; subnetId: string; image: string; diskEncrypted: boolean }
  | undefined {
  const placement = getVmPlacement(id);
  if (!placement) return undefined;
  return {
    project: 'default',
    vpcId: placement.vpcId,
    subnetId: placement.subnetId,
    image: placement.imageId,
    diskEncrypted: true,
  };
}

function nextHostOctet(subnetId: string): number {
  let count = 0;
  for (const record of records.values()) {
    if (record.placement.subnetId === subnetId) count += 1;
  }
  return 50 + (count % 190);
}

export function createVm(input: Partial<CreateVmRequest>, actorUserId: string): Vm {
  nextSeq += 1;
  const id = `vm-mock-${Date.now().toString(36)}${nextSeq.toString(36)}`;
  const subnetId = input.subnetId ?? 'subnet-prod-eu-compute-a';
  const subnet = SUBNET_BY_ID.get(subnetId);
  const vpcId = subnet?.vpcId ?? input.vpcId ?? 'vpc-prod-eu';
  const zone = subnet?.zone ?? 'eu-west-1a';
  const base = subnet ? subnet.cidr.split('/')[0]!.split('.').slice(0, 3).join('.') : '10.0.0';
  const internalIp = `${base}.${nextHostOctet(subnetId)}`;
  const vm: Vm = {
    id,
    name: input.name ?? 'unnamed-vm',
    status: 'provisioning',
    internalIp,
    zone,
    machineType: input.machineType ?? '2-4',
    vcpu: input.vcpu ?? 2,
    ramGb: input.ramGb ?? 4,
    diskGb: input.diskGb ?? 20,
    tags: input.tags,
    createdAt: new Date().toISOString(),
  };
  records.set(id, {
    vm,
    placement: { nodeId: 'node-pve-r1n01', subnetId, vpcId, imageId: input.image ?? 'img-ubuntu-2404' },
    provisioningUntil: Date.now() + PROVISION_MS,
  });
  appendAuditEntry({ action: 'vm.create', actorUserId, targetKind: 'vm', targetId: id });
  return vm;
}

export function startVm(id: string, actorUserId: string): Vm | undefined {
  const record = records.get(id);
  if (!record) return undefined;
  record.vm = { ...record.vm, status: 'running' as VmStatus };
  record.provisioningUntil = undefined;
  appendAuditEntry({ action: 'vm.start', actorUserId, targetKind: 'vm', targetId: id });
  return record.vm;
}

export function stopVm(id: string, actorUserId: string): Vm | undefined {
  const record = records.get(id);
  if (!record) return undefined;
  record.vm = { ...record.vm, status: 'stopped' as VmStatus };
  appendAuditEntry({ action: 'vm.stop', actorUserId, targetKind: 'vm', targetId: id });
  return record.vm;
}

export function deleteVm(id: string, actorUserId: string): boolean {
  const existed = records.delete(id);
  if (existed) appendAuditEntry({ action: 'vm.delete', actorUserId, targetKind: 'vm', targetId: id });
  return existed;
}
