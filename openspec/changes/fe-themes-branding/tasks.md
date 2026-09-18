# Tasks: fe-themes-branding

Numbered checklist. Each sub-section is one or more commits.

## FTB.1 — Theme registry + 3 preset themes

- [x] `web/packages/tokens/src/themes/{paper,midnight,nord}.ts`
      — each exports an OKLCH token set (one theme = one file).
- [x] `ThemeRegistry` class — registers the 3 presets, exposes
      `get(themeId)`, `apply(themeId)` (mutates
      `document.documentElement.style`), `list()`.
- [x] Unit tests: `ThemeRegistry.test.ts` (3 cases — apply,
      fallback on unknown id, list ordering).

## FTB.2 — Backend persistence + REST

- [x] `Plexor.Modules.Branding` project (Domain / Application /
      Infrastructure / Api).
- [x] `BrandingConfig` entity — global vs per-org via the
      `OrgId` column (null = global).
- [x] EF DbContext (`BrandingDbContext`) +
      `BrandingConfigConfiguration` (snake_case, schema
      `branding`).
- [x] Migration `InitBranding` via
      `dotnet ef migrations add InitBranding --context BrandingDbContext`.
- [x] `BrandingController` with the 5 endpoints (read global,
      upsert global, read per-org, upsert per-org, resolved).
- [x] FluentValidation on the upserts (`accent` is a 7-char
      hex; `logoUrl` is an absolute https URL when present;
      `customCss` ≤ 16 KiB).
- [x] `BrandingGlobalSeeder` (`IHostedService`) — idempotent.
- [x] `BootConfig` extended with `branding.global` +
      `branding.theme` fields.
- [x] Permission string `branding.update` added to the role
      permission catalog (granted to `admin` by default).

## FTB.3 — AdminBrandingPage + LiveBrandPreview

- [x] `AdminBrandingPage` route (`/admin/branding`) under
      `web/apps/console/src/features/admin/branding/`.
- [x] `LiveBrandPreview` — applies the in-progress form
      values to a preview pane via `applyTheme` +
      `applyAccent` (without persisting).
- [x] Form fields: name, logo URL, accent (color picker),
      theme preset dropdown, custom CSS textarea.
- [x] Submit calls `PUT /api/v1/branding/global`.
- [x] i18n keys for every label (`branding.admin.*`).

## FTB.4 — Custom.css escape hatch

- [x] `applyCustomCss(css : string)` — injects a `<style>`
      tag with id `plexor-custom-css` into `<head>`; re-apply
      on every theme change.
- [x] `clearCustomCss()` — removes the tag on
      `customCss = ""`.
- [x] Scope guard: only injects inside the console's root;
      does not leak to the rest of the host page (no-op when
      running in the embedded mode).
- [x] Unit tests: `applyCustomCss.test.ts` (4 cases).

## FTB.5 — FE main wiring + BootConfig consumption

- [x] `web/apps/console/src/main.tsx` — read
      `bootConfig.branding.global` + `bootConfig.branding.theme`,
      call `ThemeRegistry.apply()` +
      `resolveBranding(bootConfig)` BEFORE the React tree
      mounts (so the first paint is already branded).
- [x] `PreferencesProvider` stays unchanged (per-user accent
  override remains a separate concern).

## FTB.6 — File-organization cleanup

- [x] `Plexor.Modules.Branding.Api/Models/` split into
      `Requests/` + `Responses/` (folder-organization
      compliance).
- [x] Drop the `_ = clock;` discard pattern that snuck into
      the seeder (rule: bare `await`, no `_ =`).

## FTB.7 — BrandingGlobalSeederHostedService invariant

- [x] The seeder now UPSERTS the seeded row (idempotent)
      instead of asserting the row is present (which threw on
      cold-boot if the migrator hadn't run first).
