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
  createVm,
  createVmList,
  createVmDetail,
} from '@/shared/api';

// Deterministic mocks: same data every reload (stable UI + screenshots).
faker.seed(1337);

// A small seeded fleet so the VM list renders multiple rows instead of one.
const fleet = Array.from({ length: 8 }, () => createVm());

export const handlers: RequestHandler[] = [
  listVmsHandler(createVmList({ items: fleet, total: fleet.length, page: 1, pageSize: 20 })),
  getVmHandler(createVmDetail()),
  provisionVmHandler(createVmDetail()),
  startVmHandler(createVmDetail()),
  stopVmHandler(createVmDetail()),
  deleteVmHandler(),
];
