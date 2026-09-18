# Spec delta: fe-themes-branding (fe-themes-branding)

This file is the proposed state of
`openspec/specs/fe-themes-branding/spec.md` after the change
`openspec/changes/fe-themes-branding/` is merged. It uses the
`## ADDED Requirements` convention from OpenSpec.

When the change lands, this delta is promoted into
`openspec/specs/fe-themes-branding/spec.md` under
`## Requirements`, and the `## ADDED Requirements` heading
is removed.

## ADDED Requirements

### Requirement: Theme registry with 3 preset themes

The FE SHALL expose a `ThemeRegistry`
(`web/packages/tokens/src/registry.ts`) that registers three
preset themes in v0.1:

- `paper` — light, OKLCH-based, default for fresh installs.
- `midnight` — dark, low-glare for ops dashboards.
- `nord` — blue-tinted dark, popular among developers.

Each theme is a `.ts` file under
`web/packages/tokens/src/themes/<id>.ts` exporting a typed
`Theme` record (init-only properties) with at minimum:

- `Background : string` — `--bg-base`.
- `Surface1 / Surface2 / Surface3 : string` — card layers.
- `Ink / MutedInk : string` — text colors.
- `Primary : string` — brand accent (`--primary`).
- `Accent : string` — neutral gray (`--accent`); the brand
  red lives in `Primary`, not `Accent`.
- `StatusOk / StatusWarn / StatusErr / StatusIdle : string`
  — semantic colors (`--level-ok`, `--level-warn`, …).

`ThemeRegistry.list()` SHALL return theme ids in deterministic
order (the registration order). `ThemeRegistry.get(id)` SHALL
return `null` for unknown ids — the caller falls back to
`paper`. `ThemeRegistry.apply(id)` SHALL mutate
`document.documentElement.style` for each token and SHALL be
idempotent (repeated calls leave the DOM in the same state).

### Requirement: applyBranding merges global + per-org

The FE SHALL expose `applyBranding(branding : ResolvedBranding)`
(`web/packages/tokens/src/apply.ts`) that, in order:

1. Reads `branding.themeId` and calls `ThemeRegistry.apply(...)`.
2. Reads `branding.accent` and overrides `--primary` +
   `--ring` with the per-org / global accent (when non-null).
3. Reads `branding.customCss` and injects it as a
   `<style id="plexor-custom-css">` tag in `<head>`
   (idempotent: removes any prior tag with the same id
   before injecting).
4. Reads `branding.name` and `branding.logoUrl` and writes
   them to the `<title>` + `<link rel="icon">` (the
   document metadata is owned by the FE shell; this is the
   one place it reads from).

`applyBranding` SHALL be called BEFORE the React tree mounts
(in `web/apps/console/src/main.tsx`) so the first paint is
already branded — no flash of unstyled content.

### Requirement: BootConfig exposes the branding envelope

The backend SHALL extend the `GET /api/v1/boot` response with
a `branding` envelope:

```jsonc
{
  "branding": {
    "global": {
      "name": "Plexor",
      "logoUrl": "https://...",
      "accent": "#...",      // optional override
      "customCss": ""        // optional override (≤ 16 KiB)
    },
    "theme": {
      "defaultPresetId": "paper",
      "availablePresets": ["paper", "midnight", "nord"]
    }
  }
}
```

The boot response SHALL include the global config even when
the operator hasn't touched it (the seeder writes a baseline
on first boot).

### Requirement: Resolved branding endpoint

The backend SHALL expose `GET /api/v1/branding/resolved?orgId={id}`
returning the merged branding for an org (per-org override or
global fallback). Tenant-scoped: a user in Org X SHALL NOT
query Org Y (HTTP 404, never 200 with another tenant's
branding — same contract as every other Plexor endpoint).

### Requirement: Custom CSS escape hatch

The FE SHALL apply operator-supplied `customCss` via
`applyBranding(branding)` (see the previous requirement).
The CSS is injected as a `<style>` tag into `<head>` and is
capped at 16 KiB by the backend (FluentValidation). Operators
who try to escape the scope (e.g. `body { display: none }`)
are responsible for the result — the v0.1 design system
ships with sensible defaults that the custom CSS layers on
top of.

The custom CSS tag id `plexor-custom-css` is stable; re-applying
branding removes the prior tag before injecting the new one.

### Requirement: AdminBrandingPage + LiveBrandPreview

The FE SHALL expose an `/admin/branding` route
(`AdminBrandingPage`) under
`web/apps/console/src/features/admin/branding/`:

- Form fields: `name`, `logoUrl`, `accent` (color picker),
  `themeId` (preset dropdown), `customCss` (textarea).
- `LiveBrandPreview` — applies the form values to a sample
  page (next to the form) WITHOUT persisting; uses
  `applyBranding` with a local in-memory `ResolvedBranding`.
- Submit calls `PUT /api/v1/branding/global` and shows
  success / error toasts.
- All labels are i18n-keyed (`branding.admin.*`).

The page is gated behind the `branding.update` permission
string; missing permission renders a 403 panel.

## ADDED Key Entities

### `Theme`

`web/packages/tokens/src/themes/<id>.ts` — typed
init-only record:

```ts
export interface Theme {
  readonly id: string;                  // "paper"
  readonly label: string;               // "Paper (default)"
  readonly Background: string;
  readonly Surface1: string;
  readonly Surface2: string;
  readonly Surface3: string;
  readonly Ink: string;
  readonly MutedInk: string;
  readonly Primary: string;
  readonly Accent: string;
  readonly StatusOk: string;
  readonly StatusWarn: string;
  readonly StatusErr: string;
  readonly StatusIdle: string;
}
```

### `ResolvedBranding`

The runtime shape consumed by `applyBranding`:

```ts
export interface ResolvedBranding {
  readonly themeId: string;
  readonly name: string;
  readonly logoUrl: string;
  readonly accent: string | null;       // null = use theme's Primary
  readonly customCss: string;
}
```

### `BootConfigBrandingEnvelope`

The boot response envelope (see BootConfig requirement above):
`(global: BrandingConfig, theme: ThemeBootInfo)`. Lives on
the backend DTO (`BrandingBootEnvelope` in
`Plexor.Modules.Branding.Api.Responses`).
