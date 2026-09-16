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
import {
  deleteOrgBranding,
  getBootBranding,
  getGlobalBranding,
  getOrgBranding,
  updateGlobalBranding,
  updateOrgBranding,
} from '@/features/branding/branding-service';

const brandingService = {
  getGlobalBranding,
  updateGlobalBranding,
  getOrgBranding,
  updateOrgBranding,
  deleteOrgBranding,
  getBootBranding,
};

export interface BrandingServiceMocks {
  getGlobal: MockInstance<typeof getGlobalBranding>;
  updateGlobal: MockInstance<typeof updateGlobalBranding>;
  getOrg: MockInstance<typeof getOrgBranding>;
  updateOrg: MockInstance<typeof updateOrgBranding>;
  deleteOrg: MockInstance<typeof deleteOrgBranding>;
  getBoot: MockInstance<typeof getBootBranding>;
}

export function mockBrandingService(): BrandingServiceMocks {
  return {
    getGlobal: vi.spyOn(brandingService, 'getGlobalBranding'),
    updateGlobal: vi.spyOn(brandingService, 'updateGlobalBranding'),
    getOrg: vi.spyOn(brandingService, 'getOrgBranding'),
    updateOrg: vi.spyOn(brandingService, 'updateOrgBranding'),
    deleteOrg: vi.spyOn(brandingService, 'deleteOrgBranding'),
    getBoot: vi.spyOn(brandingService, 'getBootBranding'),
  };
}