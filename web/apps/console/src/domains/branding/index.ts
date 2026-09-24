/**
 * Public surface of the branding domain (absorbs the former 'themes'
 * feature — theme marketplace activation is a branding concern). Routes
 * import from '@/domains/branding'; internal api files stay unexported
 * outside this barrel (see .agents/docs/architecture/frontend-ddd.md).
 * The theme registry/resolution mechanism itself (@/shared/lib/themes)
 * is shared kernel, not part of this domain — see the plan doc's step 4
 * reconciliation note for why.
 */
export { brandingQueryKeys, useGlobalBranding, useOrgBranding, useBootBranding, useUpdateGlobalBranding, useUpdateOrgBranding, useDeleteOrgBranding } from './api/use-branding';
export { themeMarketplaceQueryKeys, useCommunityThemes, useActiveThemeId, useActivateTheme, useDeactivateTheme } from './api/use-community-themes';
