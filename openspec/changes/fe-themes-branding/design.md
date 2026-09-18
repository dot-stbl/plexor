# Design: fe-themes-branding

Technical decisions for the FE themes + branding work. Each
section explains the trade-off chosen over the alternatives.

## Why a separate Tokens package

`web/packages/tokens/` is a workspace member distinct from
`web/apps/console/`. The console consumes it; a future
`web/apps/admin-portal/` (the Plexor multi-tenant admin
shell) will consume it too; a future public marketing site
will also reuse the OKLCH primitives.

Splitting tokens from the app:

- **No app code in the tokens package.** It's a flat
  collection of `.ts` files exporting typed token sets.
- **No circular deps.** The app pulls tokens in via the
  workspace alias (`@plexor/tokens`); tokens don't reach
  back into the app.
- **Theme additions are 1-file PRs.** A new theme is a new
  `.ts` file + 1 entry in `ThemeRegistry.list()`.

## OKLCH over RGB / HSL

The Plexor design system commits to OKLCH throughout. Two
reasons:

1. **Predictable lightness.** OKLCH's `L` axis is
   perceptually uniform — bumping `L` from 0.4 to 0.5 makes
   the surface lighter in a way the eye reads as
   "halfway between". RGB / HSL don't have this property.
2. **Wide gamut.** OKLCH encodes colors outside the sRGB
   gamut (P3 / Rec.2020), so the system is forward-compatible
   with the next-gen displays that ship in 2026+.

The downside — `oklch()` syntax isn't supported by IE11 and
breaks one analyzer (CA-1416) — doesn't apply: Plexor targets
evergreen browsers only.

## applyTheme vs CSS class swap

Two ways to switch themes:

1. **`applyTheme(themeId)`** — JS mutates
   `document.documentElement.style.setProperty('--bg-base',
   oklch(...))` for every token. Chosen.
2. **`<html data-theme="midnight">` + CSS rules per theme**
   in `index.css`. Rejected for v0.1 because it duplicates
   the token list in CSS and forces a re-paint on every
   theme change. The JS path is one style mutation per
   token, batched in a single `requestAnimationFrame`.

Trade-off: the JS path requires the FE to be JS-loaded
before the first paint. The boot config + the `main.tsx`
hydration ensure this — see FTB.5.

## Global vs per-org branding shape

A `BrandingConfig` row has an `OrgId` column. Null = global
default; non-null = per-org override. The resolved
branding for a request is computed as:

```
resolved(orgId) = orgOverride(orgId) ?? globalDefault
```

Why nullable column instead of two tables? The two cases
share the same shape (name / logo / accent / customCss /
themeId); the only difference is the lookup key. Two tables
would double the entity maintenance burden for no
information gain.

## Custom CSS injection — escape hatch, not a feature

Operator-supplied CSS is a deliberate escape hatch for
last-mile styling the design system doesn't cover. The
trade-offs:

- **Risk.** Operator-supplied CSS can break the design
  system if misapplied. v0.1 limits the scope: only the
  console's root container is affected; the host page (if
  embedded) is not. A future sandbox iframe is Phase 5+.
- **Reward.** An operator can apply a tenant brand color
  across every page without filing a feature request.
- **Size cap.** `customCss` is capped at 16 KiB (one
  paragraph of CSS — enough for color overrides, not for
  layouts).

The Custom CSS is applied via a `<style id="plexor-custom-css">`
tag injected into `<head>`; clearing it on `customCss = ""`
is the cleanup path.

## Why `applyAccent` exists alongside `applyTheme`

`applyTheme` swaps the entire token set; `applyAccent` is a
single-property override (the `--primary` variable). The
two are separate because:

- **Theme swap** is a low-frequency operator action
  (admin clicks a dropdown, the whole site re-skins).
- **Accent override** is a per-user preference (`viewer`
  picks a different accent from the design system
  palette — the existing `PreferencesProvider` does this).
- An operator who wants a tenant-specific accent uses the
  per-org `BrandingConfig.accent` field; a user who wants
  their personal accent uses `PreferencesProvider`. The two
  layers don't collide.

## Boot config — branding envelope

`GET /api/v1/boot` returns the existing `BootConfig`
payload + a new `branding` envelope:

```jsonc
{
  // ... existing fields ...
  "branding": {
    "global": { "name": "Plexor", "logoUrl": "...", "accent": "#..." },
    "theme": { "defaultPresetId": "paper", "availablePresets": ["paper", "midnight", "nord"] }
  }
}
```

The FE reads this BEFORE mounting the React tree (in
`main.tsx`) so the first paint is branded. The
`/api/v1/branding/resolved?orgId=X` endpoint exists for
runtime re-resolution after a per-org PUT.

## Open questions deferred

- **Theme marketplace.** A separate change ships the FE
  registry + admin install UI + backend persistence +
  HMAC-signed manifest.
- **Dark / light per-page toggle.** v0.1 is
  whole-document; per-page is Phase 5+.
- **i18n of brand strings.** Phase 5+.
- **Logo upload.** Operator supplies a URL; no file
  upload in v0.1.
