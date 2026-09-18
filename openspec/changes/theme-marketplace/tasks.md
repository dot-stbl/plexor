# Tasks: theme-marketplace

Numbered checklist.

## TM.1 — Community theme registry

- [x] `CommunityTheme` interface (id, label, author,
      version, preview, tokens).
- [x] Two starter community themes:
      `midnight-violet` + `paper-warm`.
- [x] `ThemeRegistry.list()` extended with the
      community catalog (in addition to the 3 built-in
      presets).

## TM.2 — ThemeMarketplacePage

- [x] `/admin/themes/marketplace` route under
      `web/apps/console/src/features/admin/themes/`.
- [x] Grid layout with `LiveBrandPreview` per theme.
- [x] `Install` / `Activate` / `Deactivate` /
      `Uninstall` buttons.
- [x] i18n keys for every label.

## TM.3 — useInstalledThemes hook

- [x] `web/apps/console/src/features/admin/themes/hooks/useInstalledThemes.ts`.
- [x] localStorage key:
      `plexor:themes:installed:<orgId>`.
- [x] Active selection localStorage key:
      `plexor:themes:active:<orgId>`.
- [x] Hook contract: `installed`, `active`, `install`,
      `uninstall`, `activate`, `deactivate`.

## TM.4 — Wire activation into the existing applyTheme flow

- [x] On `activate(themeId)`, call
      `ThemeRegistry.apply(themeId)` so the FE immediately
      re-skins.
- [x] Persist the choice before applying (so a hard
      refresh keeps the selection).

## TM.5 — Component tests

- [x] `ThemeMarketplacePage.test.tsx` (4 cases —
      install, activate, deactivate, uninstall).
- [x] `useInstalledThemes.test.tsx` (3 cases —
      localStorage round-trip, isolation per org, missing
      key fallback).

## TM.6 — Merge

- [x] `merge theme-marketplace` commit lands on `main`.
