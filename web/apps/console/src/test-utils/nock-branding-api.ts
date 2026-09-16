/**
 * mockBrandingService — wrap every branding service function in a vi.spyOn
 * so tests can stub return values or assert call args without touching the
 * real fetch path.
 *
 * Usage:
 *   const mocks = mockBrandingService();
 *   mocks.getGlobal.mockResolvedValue({ brandName: 'Plexor', ... });
 *   await user.click(saveButton);
 *   expect(mocks.updateGlobal).toHaveBeenCalledWith({ ... });
 */
import { vi, type MockInstance } from 'vitest';
import * as service from '@/features/branding/branding-service';

export interface BrandingServiceMocks {
  getGlobal: MockInstance<(typeof service)['getGlobalBranding']>;
  updateGlobal: MockInstance<(typeof service)['updateGlobalBranding']>;
  getOrg: MockInstance<(typeof service)['getOrgBranding']>;
  updateOrg: MockInstance<(typeof service)['updateOrgBranding']>;
  deleteOrg: MockInstance<(typeof service)['deleteOrgBranding']>;
  getBoot: MockInstance<(typeof service)['getBootBranding']>;
}

export function mockBrandingService(): BrandingServiceMocks {
  return {
    getGlobal: vi.spyOn(service, 'getGlobalBranding'),
    updateGlobal: vi.spyOn(service, 'updateGlobalBranding'),
    getOrg: vi.spyOn(service, 'getOrgBranding'),
    updateOrg: vi.spyOn(service, 'updateOrgBranding'),
    deleteOrg: vi.spyOn(service, 'deleteOrgBranding'),
    getBoot: vi.spyOn(service, 'getBootBranding'),
  };
}