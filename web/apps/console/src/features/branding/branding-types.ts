/**
 * Branding types — operator-global + per-org branding configuration
 * consumed by the admin UI and the boot-config script on first paint.
 *
 * Hand-rolled mirror of the backend's Plexor.Modules.Branding.Api
 * response DTOs (GlobalThemeConfigResponse / OrgThemeConfigResponse).
 * Migration path: replace with kubb-generated types once the kubb
 * pipeline runs against the latest OpenAPI spec (the spec was
 * regenerated with the branding endpoints in commit 2; the FE codegen
 * step is a follow-up).
 */

export interface GlobalBrandingConfig {
  /** Operator-set product name. */
  brandName: string;
  /** URL or web-root-relative path of the logo. Null = default SVG mark. */
  brandLogoUrl: string | null;
  /** URL or web-root-relative path of the favicon. Null = default favicon. */
  brandFaviconUrl: string | null;
  /** Stable id of the default frontend theme preset. */
  defaultPresetId: string;
  /** Custom accent (OKLCH). Null = preset default. */
  customAccent: string | null;
  /** Last update time (UTC). */
  updatedAt: string;
}

export interface OrgBrandingConfig {
  /** Tenant scope. */
  orgId: string;
  /** Per-org theme preset override. Null = inherit. */
  presetId: string | null;
  /** Per-org custom accent (OKLCH). Null = inherit. */
  customAccent: string | null;
  /** Per-org brand name. Null = inherit. */
  brandName: string | null;
  /** Per-org logo URL. Null = inherit. */
  brandLogoUrl: string | null;
  /** Per-org favicon URL. Null = inherit. */
  brandFaviconUrl: string | null;
  /** Last update time (UTC). */
  updatedAt: string;
}

/** Wire shape for PUT /api/v1/branding/global. */
export interface UpsertGlobalBrandingRequest {
  brandName: string;
  brandLogoUrl?: string | null;
  brandFaviconUrl?: string | null;
  defaultPresetId: string;
  customAccent?: string | null;
}

/** Wire shape for PUT /api/v1/branding/org/{orgId}. */
export interface UpsertOrgBrandingRequest {
  presetId?: string | null;
  customAccent?: string | null;
  brandName?: string | null;
  brandLogoUrl?: string | null;
  brandFaviconUrl?: string | null;
}

/** Resolved boot config returned by GET /api/v1/branding/boot. */
export interface ResolvedBootBranding {
  brandName: string;
  brandLogoUrl: string | null;
  brandFaviconUrl: string | null;
  defaultPresetId: string;
  customAccent: string | null;
}