/**
 * ThemeInstallationResponse — wire shape returned by
 * GET /api/v1/branding/theme (200) and PUT /api/v1/branding/theme (200).
 *
 * Hand-written to mirror what `bun run generate` would emit from the
 * Plexor.Host openapi.json document. The kubb regeneration would
 * overwrite this file — keep the shape in lock-step with the
 * C# `ThemeInstallationResponse` class so a future regen doesn't
 * break the FE bindings.
 */
/* eslint-disable */
// @ts-nocheck

export type ThemeInstallationResponse = {
  /**
   * @type string, uuid
   */
  orgId: string;
  /**
   * @type string
   */
  themeId: string;
  /**
   * @type string
   */
  name: string;
  /**
   * @type string
   */
  version: string;
  /**
   * @type string
   */
  author: string;
  /**
   * @description Hex-encoded HMAC-SHA256 persisted on the row.
   * @type string
   */
  manifestSignature: string;
  /**
   * @type string, date-time
   */
  activatedAt: string;
  /**
   * @type string, uuid
   */
  activatedBy: string;
};
