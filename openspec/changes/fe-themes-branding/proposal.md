# Change: fe-themes-branding

## Why

v0.0 ships a single hardcoded theme (`oklch-paper` + a deep-red
`--primary` accent) and a single operator brand (the Plexor
logo + Plexor name). The console has no way for the operator
to:

1. Switch between themes without a code change.
2. Override the brand name, logo, or accent from the operator
   console.
3. Apply tenant-level theme overrides (per-org look-and-feel
   for a multi-tenant deploy).
4. Inject custom CSS for last-mile styling that the design
   system doesn't cover.

Phase 4.x (the FE-side branding work) ships a token-registry
pattern + a 3-preset catalog + an operator admin page +
custom-CSS escape hatch. The work spans three repos: the
backend (`Plexor.Modules.Branding`) for persistence, the FE
token system (`web/packages/tokens`) for the registry, and
the FE console for the admin UI.

## What

### Backend (`Plexor.Modules.Branding`)

- `BrandingConfig` entity — global + per-org branding
  (`name`, `logoUrl`, `accent`, `customCss`, `themeId`).
- REST endpoints under `/api/v1/branding`:
  - `GET /api/v1/branding/global` — read global default.
  - `PUT /api/v1/branding/global` (`branding.update`) —
    upsert global.
  - `GET /api/v1/branding/org/{orgId}` — read per-org
    override (null = inherit from global).
  - `PUT /api/v1/branding/org/{orgId}` (`branding.update`) —
    upsert per-org.
  - `GET /api/v1/branding/resolved?orgId={orgId}` — return
    the **resolved** branding (per-org + global fallback).
- `BrandingGlobalSeeder` (`IHostedService`) — seeds a
  baseline global row on first boot; idempotent.
- `BootConfig` extension — the FE's boot JSON includes
  `branding.global` + `branding.theme` so the FE can hydrate
  before the React tree mounts.

### FE tokens (`web/packages/tokens`)

- `ThemeRegistry` — a catalog of named themes with their
  OKLCH token sets (paper / midnight / nord).
- `applyTheme(themeId)` — swaps the CSS custom-property
  layer in `document.documentElement`.
- `resolveBranding(bootConfig)` — merges per-org + global
  into the runtime brand.
- `defaultPresetId : string` — set from
  `bootConfig.branding.theme.defaultPresetId`.

### FE console (`web/apps/console`)

- `AdminBrandingPage` (`/admin/branding`) — operator UI for
  editing global brand fields (name, logo URL, accent picker,
  custom CSS textarea, theme preset dropdown).
- `LiveBrandPreview` — shows the resolved branding applied to
  a sample page right next to the editor.
- Custom-CSS escape hatch — the textarea content is injected
  via `<style>` into the document `<head>` at runtime (not
  build-time). Operator-supplied CSS is sandboxed to the
  console's root only (no escape to the rest of the host).
- `BrandingGlobalSeederHostedService` invariant fix
  (commit `0c6f2c8`) — the seeded row is upserted
  idempotently instead of asserted-present.
- `Api/Models/` split into `Requests/` + `Responses/`
  (commit `7b16c74`) — file-organization cleanup mandated by
  `folder-organization.md`.

## Impact

- **`branding` capability** (new) — schema `branding`,
  owned by `Plexor.Modules.Branding`. The promoted spec is
  in `openspec/specs/branding/spec.md`.
- **`fe-themes-branding` capability** (new) — the FE side of
  the same capability: token registry, apply/resolve, custom
  CSS injection. The promoted spec is in
  `openspec/specs/fe-themes-branding/spec.md`.
- **Boot config** — the FE's `GET /api/v1/boot` response
  gains a `branding` envelope (global + theme) without
  removing the existing fields.
- **Plexor.Host/Program.cs** — registers
  `AddBrandingModule()`.
- **Plexor.Migrator** — adds `InitBranding` migration + the
  seeder.

## Out of scope

- **Theme marketplace** — the registry currently ships
  3 built-in presets. Community themes + admin install UI
  ships separately under `theme-marketplace` /
  `theme-marketplace-backend`.
- **Per-user theme overrides** — Phase 5+ lets an individual
  user override their accent (the `PreferencesProvider` for
  per-session UI tweaks already exists in
  `web/apps/console/src/shared/preferences/`).
- **Logo upload** — the operator provides a URL; we don't
  accept file uploads in v0.1.
- **i18n of brand strings** — operator-supplied name + custom
  CSS are not i18n-keyed in v0.1.
- **Dark / light per-page toggle** — the theme is
  whole-document. Per-page theming is Phase 5+.
