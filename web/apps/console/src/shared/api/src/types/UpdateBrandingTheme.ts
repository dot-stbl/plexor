/**
 * UpdateBrandingTheme — kubb-style type definitions for
 * PUT /api/v1/branding/theme.
 *
 * Hand-written to mirror what `bun run generate` would emit from the
 * Plexor.Host openapi.json document.
 */
/* eslint-disable */
// @ts-nocheck

import type { ProblemDetails } from './ProblemDetails.ts';
import type { ThemeInstallationResponse } from './ThemeInstallationResponse.ts';
import type { UpsertThemeInstallationRequest } from './UpsertThemeInstallationRequest.ts';

/**
 * @description The upserted installation row.
 */
export type UpdateBrandingTheme200 = ThemeInstallationResponse;

/**
 * @description Manifest signature invalid (RFC 7807 problem details).
 */
export type UpdateBrandingTheme400 = ProblemDetails;

/**
 * @description Cross-tenant access denied.
 */
export type UpdateBrandingTheme403 = ProblemDetails;

/**
 * @description Unknown marketplace theme id.
 */
export type UpdateBrandingTheme404 = ProblemDetails;

/**
 * @description RFC 7807 error.
 */
export type UpdateBrandingThemeError = ProblemDetails;

export type UpdateBrandingThemeMutationRequest = UpsertThemeInstallationRequest;

export type UpdateBrandingThemeMutationResponse = UpdateBrandingTheme200;

export type UpdateBrandingThemeMutation = {
  Request: UpdateBrandingThemeMutationRequest;
  Response: UpdateBrandingTheme200;
  Errors: UpdateBrandingTheme400 | UpdateBrandingTheme403 | UpdateBrandingTheme404;
};
