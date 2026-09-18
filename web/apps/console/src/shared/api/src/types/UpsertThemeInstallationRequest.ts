/**
 * UpsertThemeInstallationRequest — wire shape for PUT /api/v1/branding/theme.
 *
 * Hand-written to mirror what `bun run generate` would emit from the
 * Plexor.Host openapi.json document. The body is just the themeId;
 * the host looks up the canonical manifest in its bundled
 * CommunityThemeRegistry and signs it before persisting.
 */
/* eslint-disable */
// @ts-nocheck

export type UpsertThemeInstallationRequest = {
  /**
   * @type string
   */
  themeId: string;
};
