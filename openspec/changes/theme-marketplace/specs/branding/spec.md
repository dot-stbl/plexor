# Spec delta: branding (theme-marketplace)

This file is the additive delta for the branding capability
introduced by the change `openspec/changes/theme-marketplace/`.
Existing branding Requirements are unchanged — only new
requirements are added.

When the change lands, this delta is promoted into
`openspec/specs/branding/spec.md` under `## Requirements`,
and the `## ADDED Requirements` heading is removed.

## ADDED Requirements

### Requirement: Community theme registry

The FE SHALL expose a community theme registry
(`web/packages/tokens/src/community/registry.ts`) as a
typed catalog of `CommunityTheme` entries. Each entry
carries:

- `id : string` — stable theme id (`midnight-violet`).
- `label : string` — human label (`Midnight Violet`).
- `author : string` — author / publisher name.
- `version : string` — semver (`1.0.0`).
- `preview : { light: string, dark: string }` — preview
  swatches used by `LiveBrandPreview`.
- `tokens : Theme` — OKLCH token set (same shape as the
  built-in presets).

Two starter community themes ship in v0.1:
`midnight-violet` + `paper-warm`. The registry is a
static module export — community themes ship in the FE
bundle. Remote download lands in the
`theme-marketplace-backend` follow-up.

### Requirement: ThemeRegistry.list returns built-in + community

`ThemeRegistry.list()`
(`web/packages/tokens/src/registry.ts`) SHALL return the
union of:

- The 3 built-in presets (`paper`, `midnight`, `nord`).
- The community catalog (the 2 starter themes in v0.1).

The order SHALL be: built-in first, community second.
`get(themeId)` SHALL match either namespace — `paper` and
`midnight-violet` are both resolvable.

### Requirement: useInstalledThemes hook

The FE SHALL expose
`useInstalledThemes(orgId)`
(`web/apps/console/src/features/admin/themes/hooks/useInstalledThemes.ts`)
with the following contract:

```ts
export function useInstalledThemes(orgId: string): {
  installed: CommunityTheme[];     // currently installed for the org
  active: string | null;           // active theme id, null = none
  install: (theme: CommunityTheme) => void;
  uninstall: (themeId: string) => void;
  activate: (themeId: string) => void;
  deactivate: () => void;
};
```

The hook SHALL persist state in localStorage under two
keys per org:

- `plexor:themes:installed:<orgId>` — JSON array of
  `{ id, version }` (the installed list).
- `plexor:themes:active:<orgId>` — JSON string (the
  active theme id) or absent.

The hook SHALL be backed by `useSyncExternalStore` so
the marketplace UI reflects localStorage changes from
other tabs in the same browser.

The hook SHALL silently ignore a stale `active` pointer
(theme was uninstalled): the hook returns
`active = null` and the FE falls back to
`bootConfig.branding.theme.defaultPresetId`.

### Requirement: ThemeMarketplacePage admin UI

The FE SHALL expose `/admin/themes/marketplace`
(`ThemeMarketplacePage`) under
`web/apps/console/src/features/admin/themes/`. The page
is gated on `branding.update` (same permission as
`AdminBrandingPage`).

The page renders:

- Header with the page title + count of installed themes.
- Grid of `CommunityTheme` cards (one per registry
  entry). Each card carries:
  - Theme label + author + version.
  - `LiveBrandPreview` (the existing branding preview
    component, see `fe-themes-branding`).
  - Status pill: `Not installed` / `Installed` /
    `Active`.
  - Action button: `Install` / `Activate` /
    `Deactivate` / `Uninstall`.
- Empty state when the registry is empty.

All labels are i18n-keyed (`themes.marketplace.*`).

### Requirement: Activation applies immediately

`useInstalledThemes.activate(themeId)` SHALL:

1. Validate `themeId` is in the installed list (otherwise
   throws `InvalidThemeError`).
2. Persist `themeId` to
   `plexor:themes:active:<orgId>`.
3. Call `ThemeRegistry.apply(themeId)` so the FE
   immediately re-skins.

`deactivate()` SHALL remove the active pointer and apply
the org's `bootConfig.branding.theme.defaultPresetId`.

### Requirement: Persistence survives refresh

A user who activates a theme and refreshes the page SHALL
see the same theme on reload. The hook reads from
localStorage on mount and applies the active theme
before the first paint (via the `applyBranding` flow
described in `fe-themes-branding`).
