/**
 * mockAuditService — wrap the audit service function in a vi.spyOn so tests
 * can stub the rows the page renders or assert the params it was called with.
 *
 * Usage:
 *   const mocks = mockAuditService();
 *   mocks.fetch.mockResolvedValue([entry1, entry2]);
 *   await user.click(applyButton);
 *   expect(mocks.fetch).toHaveBeenCalledWith(expect.objectContaining({ action: 'x' }));
 */
import { vi, type MockInstance } from 'vitest';
import { fetchAudit } from '@/features/audit/audit-service';

const auditService = { fetchAudit };

export interface AuditServiceMocks {
  fetch: MockInstance<typeof fetchAudit>;
}

export function mockAuditService(): AuditServiceMocks {
  return {
    fetch: vi.spyOn(auditService, 'fetchAudit'),
  };
}