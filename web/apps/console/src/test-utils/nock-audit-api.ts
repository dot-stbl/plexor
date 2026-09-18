/**
 * mockAuditService — wraps the kubb-generated audit client function in
 * a vi.spyOn so tests can stub the rows the page renders or assert
 * the params it was called with.
 *
 * After the kubb regen, the page reads via useAudit (TanStack Query
 * hook) which calls the kubb-generated getAudit client. Spying on
 * getAudit is the equivalent of the hand-rolled vi.spyOn(service, ...)
 * pattern that was here before.
 *
 * Usage:
 *   const mocks = mockAuditService();
 *   mocks.fetch.mockResolvedValue([entry1, entry2]);
 *   await user.click(applyButton);
 *   expect(mocks.fetch).toHaveBeenCalledWith(
 *     expect.objectContaining({ action: 'x' }),
 *     expect.anything(),  // kubb config (signal, etc.)
 *   );
 */
import { vi, type MockInstance } from 'vitest';
import * as getAuditModule from '@/shared/api/src/client/getAudit';

export interface AuditServiceMocks {
  fetch: MockInstance<(typeof getAuditModule)['getAudit']>;
}

export function mockAuditService(): AuditServiceMocks {
  return {
    fetch: vi.spyOn(getAuditModule, 'getAudit'),
  };
}
