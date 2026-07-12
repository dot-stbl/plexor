// MSW request handlers — composed from kubb-generated per-operation factories,
// fed with kubb-generated faker fixtures. Hand-maintained: add one line per new
// endpoint (or regenerate + append). Survives codegen `clean:true` (lives outside ./src).
import type { RequestHandler } from 'msw';
import { faker } from '@faker-js/faker';
import {
  listVmsHandler,
  getVmHandler,
  provisionVmHandler,
  startVmHandler,
  stopVmHandler,
  deleteVmHandler,
  createVmList,
  createVmDetail,
} from '@/shared/api';

// Deterministic mocks: same data every reload (stable UI + screenshots).
faker.seed(1337);

// Hand-curated fleet so the list renders a realistic mix of statuses.
// Names + IDs + IPs are deterministic; the rest comes from kubb factories.
const FLEET = [
  { id: 'vm-a8c91f2e', name: 'web-prod-01', status: 'running',      ip: '10.128.1.10', zone: 'eu-central-1', vcpu: 4, ram: 8,  disk: 80  },
  { id: 'vm-b7d40e1a', name: 'api-prod-01', status: 'running',      ip: '10.128.1.11', zone: 'eu-central-1', vcpu: 4, ram: 8,  disk: 60  },
  { id: 'vm-c2f8a039', name: 'worker-01',    status: 'running',      ip: '10.128.2.20', zone: 'eu-central-1', vcpu: 2, ram: 4,  disk: 40  },
  { id: 'vm-9e1b3c47', name: 'db-replica-01',status: 'running',      ip: '10.128.3.5',  zone: 'eu-central-1', vcpu: 8, ram: 32, disk: 500 },
  { id: 'vm-3a7c5d12', name: 'cache-01',     status: 'running',      ip: '10.128.4.7',  zone: 'eu-central-1', vcpu: 2, ram: 16, disk: 30  },
  { id: 'vm-6f8d22b8', name: 'build-runner', status: 'error',        ip: '10.128.5.3',  zone: 'eu-central-1', vcpu: 4, ram: 8,  disk: 100 },
  { id: 'vm-1b9e4f73', name: 'staging-api',  status: 'stopped',      ip: '10.128.6.12', zone: 'eu-central-1', vcpu: 2, ram: 4,  disk: 40  },
  { id: 'vm-4d2a89e1', name: 'ml-trainer',   status: 'provisioning', ip: '10.128.7.4',  zone: 'eu-central-1', vcpu: 8, ram: 64, disk: 250 },
] as const;

const fleet = FLEET.map((vm) => ({
  id: vm.id,
  name: vm.name,
  status: vm.status,
  internalIp: vm.ip,
  zone: vm.zone,
  machineType: `${vm.vcpu}-${vm.ram}`,
  vcpu: vm.vcpu,
  ramGb: vm.ram,
  diskGb: vm.disk,
  createdAt: faker.date.past().toISOString(),
}));

export const handlers: RequestHandler[] = [
  listVmsHandler(createVmList({ items: fleet, total: fleet.length, page: 1, pageSize: 20 })),
  getVmHandler(createVmDetail()),
  provisionVmHandler(createVmDetail()),
  startVmHandler(createVmDetail()),
  stopVmHandler(createVmDetail()),
  deleteVmHandler(),
];