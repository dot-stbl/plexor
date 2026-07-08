// Browser MSW worker — started from main.tsx only when VITE_USE_MOCKS=true.
import { setupWorker } from 'msw/browser';
import { http, passthrough, type HttpHandler } from 'msw';
import { faker } from '@faker-js/faker';
import { createVmList, createVmDetail } from '@/shared/api';

// MSW intercepts every fetch as a service worker. The kubb-generated
// handlers use `*/vms` wildcards — they were designed for an API gateway
// prefix (e.g. `https://api.plexor.dev/vms`) but in this build the
// requests go to relative paths like `/vms`. In a Vite SPA that means
// MSW intercepts:
//   - the API call: GET /vms                       ← intercept (good)
//   - the route load: GET /vms/new, /clusters/$id  ← intercept (BAD)
//
// We hard-code handlers scoped to the actual VM API surface. Anything
// that doesn't match falls through to the real network (Vite dev
// server, HMR, asset loads, route navigation) so TanStack Router works.

// Same deterministic fleet the old handlers.ts produced, kept here
// to avoid a circular import with `mocks/handlers.ts` (the worker is
// what `handlers.ts` itself wires up).
faker.seed(1337);

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

const handlers: HttpHandler[] = [
  http.get('/vms', () =>
    Response.json(
      createVmList({ items: fleet, page: 1, pageSize: 20 }),
    ),
  ),
  http.get<{ id: string }>('/vms/:id', ({ params }) => {
    const vm = fleet.find((v) => v.id === params.id);
    if (!vm) return new Response('Not found', { status: 404 });
    return Response.json(createVmDetail(vm));
  }),
  // Pass through anything else — TanStack Router SPA navigation,
  // /clusters, /vms/new, asset loads, HMR pings, etc.
  http.all('*', () => passthrough()),
];

export const worker = setupWorker(...handlers);