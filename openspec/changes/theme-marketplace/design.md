# Design: theme-marketplace

Technical decisions for the community theme marketplace
(MVP).

## Why localStorage first, backend second

The MVP shape is FE-only with localStorage persistence.
Three reasons:

1. **Time-to-MVP.** Shipping the registry + admin UI is
   the visible value; persisting in the host requires a
   schema migration + REST endpoints + audit integration.
   The MVP validates the user-facing model without the
   backend commitment.
2. **Backend seam is the same hook.** `useInstalledThemes`
   exposes `install / uninstall / activate / deactivate`.
   The backend swap is replacing the localStorage
   `get/set/remove` with an API call — the hook's contract
   is unchanged, the UI is unchanged.
3. **No silent breakage.** A user who refreshes the page
   before backend persistence lands keeps their theme
   choice (localStorage survives the refresh). Once
   backend lands, the user's localStorage entry is
   uploaded as the first install.

The hook contract is the seam. The localStorage
implementation is the MVP; the backend implementation is
the production path.

## Why a separate `community/registry.ts` file

The 3 built-in presets ship in `web/packages/tokens/src/themes/`
(typed token sets, one file per preset). The community
themes ship in
`web/packages/tokens/src/community/registry.ts` (a single
file with multiple `CommunityTheme` exports). Reasons:

- **Different lifecycle.** Built-in presets ship with
  Plexor releases; community themes ship outside the
  release cycle (once the backend lands).
- **Different review surface.** Built-in presets get
  Plexor design review; community themes get
  marketplace-style review (HMAC signature).
- **Different runtime.** Built-in presets are always
  available; community themes require an install step.

The two namespaces are joined at the registry level —
`ThemeRegistry.list()` returns both — but the source
files are distinct.

## Two starter themes — why exactly two

Two is the minimum to validate the "multiple themes"
mental model without bloating the bundle. `midnight-violet`
+ `paper-warm` are deliberately different from the
built-in presets (the violet / amber accents distinguish
them visually from the 3 system presets). When the
backend lands, the registry grows — the marketplace UI
is built for N themes, not two.

## localStorage schema

Two keys per org:

```
plexor:themes:installed:<orgId>  →  JSON array of { id, version }
plexor:themes:active:<orgId>     →  string (themeId) | null
```

The installed list stores the minimum needed to verify
the active theme is still installed. The full theme
metadata comes from `ThemeRegistry.list()` (the source
of truth); the localStorage entry is a pointer.

A stale `active` pointer (the theme was uninstalled) is
silently ignored — the hook returns `active = null` and
the FE falls back to the org's resolved branding from
`bootConfig.branding.theme.defaultPresetId`.

## Why `useInstalledThemes` is a hook, not a service

The activation state is per-org, per-session, and
client-side. A hook (vs a service singleton) is the
right shape because:

- **localStorage is the persistence layer** (no API call
  required).
- **The hook needs to be reactive** — the marketplace UI
  re-renders when the install list changes.
- **Per-org isolation** — the hook is called with `orgId`
  from the auth context; each org has its own
  localStorage namespace.

A `useSyncExternalStore` (React 18+) backs the hook so the
marketplace UI reflects localStorage changes from other
tabs.

## Marketplace page — `/admin/themes/marketplace`

The admin page lives under
`web/apps/console/src/features/admin/themes/`. The route
is gated on `branding.update` — the same permission the
existing `AdminBrandingPage` requires. The page renders:

- **Header** — "Theme Marketplace" + count of installed
  themes.
- **Grid** — one card per `CommunityTheme` in
  `ThemeRegistry.list()`. Each card carries:
  - Theme label + author + version.
  - `LiveBrandPreview` (the existing branding preview
    component, see `fe-themes-branding`).
  - Status pill (`Not installed` / `Installed` /
    `Active`).
  - Action button (`Install` / `Activate` /
    `Deactivate` / `Uninstall`).
- **Empty state** — "No community themes available" when
  the catalog is empty.

The page is i18n-keyed (`themes.marketplace.*`).

## Open questions deferred (lands in `theme-marketplace-backend`)

- **Backend persistence** — the
  `theme-marketplace-backend` change replaces the
  localStorage with `ThemeInstallation` rows + REST
  endpoints + HMAC-signed manifest verification.
- **Audit emission** — `theme.installed.activated` /
  `theme.installed.deactivated` events land with the
  backend change (commit `e29fdc7`).
- **Remote registry / download path** — the
  `theme-marketplace-backend` change ships the signed
  manifest the host verifies on activation.
- **Per-user (not per-org) theme overrides** — Phase 5+.
- **Theme marketplace UI for non-admin users** — Phase 5+
  via a `themes.use` permission.
