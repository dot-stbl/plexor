/**
 * Branding fixtures — shared by MSW handlers (`shared/api/mocks/handlers.ts`)
 * and the launcher SUMMARY card (for the "Plexor" brand line).
 *
 * Operator globals + per-org overrides mirror the v1 admin/branding
 * page. The launcher doesn't render the brand name itself (that comes
 * from `getBootConfig()`), but the audit + theme marketplace entries
 * share these fixtures so a single edit updates all surfaces.
 */

import type {
  GlobalThemeConfigResponse,
  OrgBrandingConfigResponse,
} from '@/shared/api';

/** Operator global defaults — applied to every org that hasn't overridden. */
export const GLOBAL_BRANDING: GlobalThemeConfigResponse = {
  brandName: 'Plexor',
  brandLogoUrl: null,
  brandFaviconUrl: null,
  defaultPresetId: 'plexor-default-light',
  customAccent: null,
  updatedAt: '2026-09-12T08:00:00Z',
};

/** Per-org override — single org in v0.1 single-tenant deploy. */
export const ORG_BRANDING: OrgBrandingConfigResponse = {
  orgId: '00000000-0000-0000-0000-000000000001',
  presetId: null,
  customAccent: null,
  brandName: null,
  brandLogoUrl: null,
  brandFaviconUrl: null,
  updatedAt: '2026-09-12T08:00:00Z',
};

/** Get the operator-default brand — used by the launcher / boot config
 *  consumers that only need the default brand (name, preset). */
export function getGlobalBranding(): GlobalThemeConfigResponse {
  return { ...GLOBAL_BRANDING };
}

/** Per-org lookup; v0.1 single-org so this is always the same record. */
export function getOrgBranding(_orgId: string): OrgBrandingConfigResponse {
  return { ...ORG_BRANDING };
}
