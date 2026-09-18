/**
 * DeleteBrandingTheme — kubb-style type definitions for
 * DELETE /api/v1/branding/theme.
 *
 * Hand-written to mirror what `bun run generate` would emit from the
 * Plexor.Host openapi.json document.
 */
/* eslint-disable */
// @ts-nocheck

import type { ProblemDetails } from './ProblemDetails.ts';

/**
 * @description Reset (or already absent).
 */
export type DeleteBrandingTheme204 = any;

/**
 * @description Cross-tenant access denied.
 */
export type DeleteBrandingTheme403 = ProblemDetails;

/**
 * @description RFC 7807 error.
 */
export type DeleteBrandingThemeError = ProblemDetails;

export type DeleteBrandingThemeMutationResponse = DeleteBrandingTheme204;

export type DeleteBrandingThemeMutation = {
  Response: DeleteBrandingTheme204;
  Errors: DeleteBrandingTheme403;
};
