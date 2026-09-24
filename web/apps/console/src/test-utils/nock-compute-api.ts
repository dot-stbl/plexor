/**
 * mockProvisionVmService — wraps the kubb-generated provision-VM client
 * function in a vi.spyOn so the create-VM wizard's tests can stub a
 * successful VmDetail or reject with an AxiosError-shaped 409/422/5xx,
 * without a real network call. Same pattern as `nock-audit-api.ts` /
 * `nock-auth-api.ts` — `useCreateVm` (which wraps kubb's
 * `useProvisionVm`) calls the kubb-generated `provisionVm` client;
 * spying on that client function is the seam.
 *
 * Usage:
 *   const mocks = mockProvisionVmService();
 *   mocks.provision.mockResolvedValue(makeVmDetail());
 *   // or:
 *   mocks.provision.mockRejectedValue({ response: { status: 409, data: {...} } });
 */
import { vi, type MockInstance } from 'vitest';
import * as provisionVmModule from '@/shared/api/src/client/provisionVm';

export interface ProvisionVmServiceMocks {
  provision: MockInstance<(typeof provisionVmModule)['provisionVm']>;
}

export function mockProvisionVmService(): ProvisionVmServiceMocks {
  return {
    provision: vi.spyOn(provisionVmModule, 'provisionVm'),
  };
}
