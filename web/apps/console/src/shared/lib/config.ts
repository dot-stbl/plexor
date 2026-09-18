/**
 * Window-level config injected by the host at boot time.
 *
 * The Plexor backend renders the operator's branding + theme defaults
 * into a `<script>` tag in the served HTML before first paint, so the
 * console can read them from `window.__PLEXOR_CONFIG__` without a
 * round-trip. For dev (vite, no host) the window object is missing or
 * empty, and `getBootConfig()` returns the defaults below.
 *
 * The merge is one level deep — a partial override (e.g. only
 * `brand.name`) falls back to the default for the rest. Two levels
 * is enough for the v1 shape; if a future commit needs deeper
 * nesting, replace the spread with a real deep-merge helper.
 *
 * Schema — `PlexorBootConfig` (v1):
 *
 *   brand.name         string                       "Plexor"
 *   brand.logoUrl      string | null                "/brand.svg" or null
 *   brand.faviconUrl   string | null                "/favicon.svg" or null
 *   theme.defaultPresetId string                     "plexor-default-light"
 *   branding.global    GlobalBrandingConfig        operator defaults
 *   branding.org       OrgBrandingConfig | null    per-org override (null = no override)
 *
 * The nested `branding` section mirrors the backend's IBrandingService
 * resolved view (commit 2 + commit 4). The flat `brand.*` /
 * `theme.*` keys stay for backward compatibility with the v1.0
 * sidebar / favicon script (commit db0de5c).
 */
import type {
  GlobalThemeConfigResponse,
  OrgBrandingConfigResponse,
} from '@/shared/api';

export interface PlexorBootConfig {
  readonly brand: {
    readonly name: string;
    readonly logoUrl: string | null;
    readonly faviconUrl: string | null;
  };
  readonly theme: {
    readonly defaultPresetId: string;
  };
  /**
   * Resolved branding at boot time. Backend's GET /api/v1/branding/boot
   * merges the operator-global row + per-org override; null fields in
   * the org override fall back to the global default. Undefined when
   * the host hasn't shipped a branding section yet (dev / older host).
   */
  readonly branding?: {
    readonly global: GlobalThemeConfigResponse;
    readonly org: OrgBrandingConfigResponse | null;
  };
}

declare global {
  interface Window {
    __PLEXOR_CONFIG__?: Partial<PlexorBootConfig>;
  }
}

const DEFAULT_CONFIG: PlexorBootConfig = {
  brand: {
    name: 'Plexor',
    logoUrl: null,
    faviconUrl: null,
  },
  theme: {
    defaultPresetId: 'plexor-default-light',
  },
};

/**
 * Read the boot config. Merges `window.__PLEXOR_CONFIG__` over the
 * defaults; missing keys fall back. Unknown keys are ignored — the
 * host can ship extras without breaking the console.
 */
export function getBootConfig(): PlexorBootConfig {
  const boot = (typeof window === 'undefined' ? undefined : window.__PLEXOR_CONFIG__) ?? {};
  return {
    brand: { ...DEFAULT_CONFIG.brand, ...boot.brand },
    theme: { ...DEFAULT_CONFIG.theme, ...boot.theme },
    branding: boot.branding
      ? {
          global: {
            ...boot.branding.global,
          },
          org: boot.branding.org ?? null,
        }
      : undefined,
  };
}