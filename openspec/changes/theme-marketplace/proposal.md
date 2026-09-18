# Change: theme-marketplace

## Why

`fe-themes-branding` ships a token registry with three
presets (`paper`, `midnight`, `nord`). Operators can apply
the presets but cannot add a new preset without shipping
a code change — every theme is a TypeScript file in
`web/packages/tokens/src/themes/`. A multi-tenant deploy
with many operators (or a community of designers) wants
to publish their own themes without a Plexor release.

Phase "theme-marketplace" ships a community theme registry
+ admin UI for installing + activating community themes
per-org. v0.1 is MVP: two starter community themes
shipped in the registry, the admin UI lets an operator
install + activate + deactivate them, and the active
selection persists per-org in `localStorage` (no backend
persistence — that's the follow-up
`theme-marketplace-backend` change).

## What

### ThemeRegistry (FE)

`web/packages/tokens/src/community/registry.ts` — a
catalog of community themes:

```ts
export interface CommunityTheme {
  readonly id: string;                    // "midnight-violet"
  readonly label: string;                 // "Midnight Violet"
  readonly author: string;                // "Plexor Community"
  readonly version: string;               // "1.0.0"
  readonly preview: { light: string; dark: string };
  readonly tokens: Theme;                 // OKLCH set
}
```

Two community themes ship in v0.1:

- `midnight-violet` — dark, violet primary accent.
- `paper-warm` — light, warm amber accent.

The registry is a static module export; community themes
ship in the FE bundle. v0.1 has no remote registry /
download path.

### ThemeMarketplacePage

`web/apps/console/src/features/admin/themes/` — the
admin UI for browsing + installing + activating
community themes:

- `/admin/themes/marketplace` — grid of available
  community themes with `LiveBrandPreview`.
- Click `Install` → adds the theme to the org's installed
  list (localStorage).
- Click `Activate` → applies the theme to the current
  org via `ThemeRegistry.apply(themeId)` + persists the
  choice in localStorage under
  `plexor:themes:active:<orgId>`.

### LocalStorage persistence (MVP)

`useInstalledThemes(orgId)` hook (`web/apps/console/src/features/admin/themes/hooks/useInstalledThemes.ts`):

```ts
export function useInstalledThemes(orgId: string): {
  installed: CommunityTheme[];
  active: string | null;
  install: (theme: CommunityTheme) => void;
  uninstall: (themeId: string) => void;
  activate: (themeId: string) => void;
  deactivate: () => void;
};
```

The hook wraps `localStorage` with a JSON-typed
get/set/remove and exposes the canonical
"install / activate" mental model. The hook's contract
is the seam the backend persistence (`theme-marketplace-backend`)
plugs into without changing the UI.

### Audit emission

The theme marketplace emits two audit events
(`theme.installed.activated` / `theme.installed.deactivated`)
in the follow-up `theme-marketplace-audit` change
(commit `e29fdc7`); the MVP localStorage change ships
without audit emission (the events are emitted when the
backend persistence lands).

## Impact

- **`branding` capability** — additive delta:
  - Community theme registry (FE-only in v0.1).
  - `ThemeMarketplacePage` admin UI.
  - `useInstalledThemes` hook.
  See `openspec/changes/theme-marketplace/specs/branding/spec.md`.

## Out of scope (lands in `theme-marketplace-backend`)

- **Backend persistence** — the
  `theme-marketplace-backend` change ships a
  `ThemeInstallation` entity + REST endpoints +
  HMAC-signed manifest verification so community themes
  can be published outside the FE bundle.
- **Remote registry / download path** — the
  `theme-marketplace-backend` change ships the signed
  manifest the host verifies on activation.
- **Per-user (not per-org) theme overrides** — Phase 5+.
- **Theme versioning + auto-update** — Phase 5+ with
  the signed manifest.
- **Theme marketplace UI for non-admin users** — the
  marketplace is admin-gated in v0.1 (`branding.update`
  permission).
