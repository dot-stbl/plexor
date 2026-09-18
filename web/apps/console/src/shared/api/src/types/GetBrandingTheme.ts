/**
 * GetBrandingTheme — kubb-style type definitions for
 * GET /api/v1/branding/theme.
 *
 * Hand-written to mirror what `bun run generate` would emit from the
 * Plexor.Host openapi.json document.
 */
/* eslint-disable */
// @ts-nocheck

import type { ProblemDetails } from './ProblemDetails.ts';
import type { ThemeInstallationResponse } from './ThemeInstallationResponse.ts';

/**
 * @description The active marketplace installation.
 */
export type GetBrandingTheme200 = ThemeInstallationResponse;

/**
 * @description Cross-tenant access denied.
 */
export type GetBrandingTheme403 = ProblemDetails;

/**
 * @description The marketplace theme has not been activated for the tenant.
 */
export type GetBrandingTheme404 = ProblemDetails;

/**
 * @description RFC 7807 error.
 */
export type GetBrandingThemeError = ProblemDetails;

export type GetBrandingThemeQueryResponse = GetBrandingTheme200;

export type GetBrandingThemeQuery = {
  Response: GetBrandingTheme200;
  Errors: GetBrandingTheme403 | GetBrandingTheme404;
};
