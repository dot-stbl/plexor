/**
 * mockBrandingService — wraps the kubb-generated branding client functions
 * in vi.spyOn handles so tests can stub return values or assert call
 * args without touching the real fetch path.
 *
 * After the kubb regen, the page reads / writes via TanStack Query
 * hooks (useGlobalBranding, useUpdateGlobalBranding, ...) which call
 * the kubb-generated client functions (getBrandingGlobal,
 * updateBrandingGlobal, ...). Spying on those client functions is the
 * equivalent of the hand-rolled vi.spyOn(service, ...) pattern that
 * was here before — same shape, same semantics, swap target.
 *
 * Usage:
 *   const mocks = mockBrandingService();
 *   mocks.getGlobal.mockResolvedValue({ brandName: 'Plexor', ... });
 *   await user.click(saveButton);
 *   expect(mocks.updateGlobal).toHaveBeenCalledWith(
 *     { brandName: 'Plexor', ... },  // request body
 *     expect.anything(),              // kubb config (signal, etc.)
 *   );
 */
import { vi, type MockInstance } from 'vitest';
import * as getBrandingGlobalModule from '@/shared/api/src/client/getBrandingGlobal';
import * as updateBrandingGlobalModule from '@/shared/api/src/client/updateBrandingGlobal';
import * as getBrandingOrgModule from '@/shared/api/src/client/getBrandingOrg';
import * as updateBrandingOrgModule from '@/shared/api/src/client/updateBrandingOrg';
import * as deleteBrandingOrgModule from '@/shared/api/src/client/deleteBrandingOrg';
import * as getBrandingBootModule from '@/shared/api/src/client/getBrandingBoot';

export interface BrandingServiceMocks {
  getGlobal: MockInstance<(typeof getBrandingGlobalModule)['getBrandingGlobal']>;
  updateGlobal: MockInstance<(typeof updateBrandingGlobalModule)['updateBrandingGlobal']>;
  getOrg: MockInstance<(typeof getBrandingOrgModule)['getBrandingOrg']>;
  updateOrg: MockInstance<(typeof updateBrandingOrgModule)['updateBrandingOrg']>;
  deleteOrg: MockInstance<(typeof deleteBrandingOrgModule)['deleteBrandingOrg']>;
  getBoot: MockInstance<(typeof getBrandingBootModule)['getBrandingBoot']>;
}

export function mockBrandingService(): BrandingServiceMocks {
  return {
    getGlobal: vi.spyOn(getBrandingGlobalModule, 'getBrandingGlobal'),
    updateGlobal: vi.spyOn(updateBrandingGlobalModule, 'updateBrandingGlobal'),
    getOrg: vi.spyOn(getBrandingOrgModule, 'getBrandingOrg'),
    updateOrg: vi.spyOn(updateBrandingOrgModule, 'updateBrandingOrg'),
    deleteOrg: vi.spyOn(deleteBrandingOrgModule, 'deleteBrandingOrg'),
    getBoot: vi.spyOn(getBrandingBootModule, 'getBrandingBoot'),
  };
}
