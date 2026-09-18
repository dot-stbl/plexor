# Capability: branding

## Purpose

Operator-facing branding for the Plexor console. The
branding capability covers:

1. **Global + per-org brand fields** — name, logo URL,
   accent color, custom CSS escape hatch, theme preset
   id. The host persists the config; the FE resolves per-
   org + global fallback before the React tree mounts so
   the first paint is already branded.
2. **Theme registry** — three built-in presets
   (`paper`, `midnight`, `nord`) plus a community
   catalog (initially 2 starter themes:
   `midnight-violet`, `paper-warm`).
3. **Theme marketplace** — admin UI for browsing +
   installing + activating community themes per-org,
   backed by a `ThemeInstallation` entity with HMAC-
   SHA256 manifest verification.

This capability is owned by `Plexor.Modules.Branding`
(the backend) and `web/packages/tokens` (the FE token
system). Schema name in SQL and migrations: `branding`.
C# concept names: `BrandingConfig`, `ThemeInstallation`,
`CommunityTheme`, `Theme`, `ResolvedBranding`.

## Requirements

### Requirement: BrandingConfig entity

The system SHALL expose a `BrandingConfig` entity
(`Plexor.Modules.Branding.Domain.Entities.BrandingConfig`,
schema `branding.branding_configs`). One row per org or
one global row (`OrgId` null = global).

`BrandingConfig` SHALL carry:

- `Id : Guid` — UUID v7 PK.
- `OrgId : Guid?` — null = global; non-null = per-org
  override.
- `Name : string` — operator label (`Plexor`).
- `LogoUrl : string?` — absolute https URL (optional).
- `Accent : string?` — 7-char hex override
  (`#RRGGBB`).
- `CustomCss : string?` — operator-supplied CSS
  (≤ 16 KiB).
- `ThemeId : string` — registry key (`paper`,
  `midnight-violet`).
- `CreatedAt : DateTimeOffset`, `UpdatedAt :
  DateTimeOffset`.

`UNIQUE (OrgId)` (one row per org + one global row).

### Requirement: REST endpoints

The system SHALL expose the following endpoints, all
behind `Plexor.Shared.Authorization.RequirePermissionAttribute`:

- `GET /api/v1/branding/global` — read global default.
- `PUT /api/v1/branding/global` (`branding.update`) —
  upsert global.
- `GET /api/v1/branding/org/{orgId}` — read per-org
  override (null = inherit from global).
- `PUT /api/v1/branding/org/{orgId}` (`branding.update`)
  — upsert per-org.
- `GET /api/v1/branding/resolved?orgId={orgId}` —
  return the merged branding (per-org + global fallback).

Tenant-scoped: a user in Org X SHALL NOT view or mutate
Org Y's branding.

### Requirement: Theme registry with built-in + community

The FE SHALL expose a `ThemeRegistry`
(`web/packages/tokens/src/registry.ts`) that registers
three preset themes in v0.1:

- `paper` — light, OKLCH-based, default for fresh
  installs.
- `midnight` — dark, low-glare for ops dashboards.
- `nord` — blue-tinted dark, popular among developers.

Each built-in theme is a `.ts` file under
`web/packages/tokens/src/themes/<id>.ts` exporting a
typed `Theme` record (init-only properties).

In addition, the FE SHALL expose a community theme
registry
(`web/packages/tokens/src/community/registry.ts`) as a
typed catalog of `CommunityTheme` entries. Each entry
carries `id`, `label`, `author`, `version`, `preview`,
and `tokens : Theme`. Two starter community themes ship
in v0.1: `midnight-violet` + `paper-warm`.

`ThemeRegistry.list()` SHALL return the union of built-in
+ community in deterministic order. `ThemeRegistry.get(id)`
SHALL match either namespace.

### Requirement: applyBranding merges global + per-org

The FE SHALL expose `applyBranding(branding :
ResolvedBranding)`
(`web/packages/tokens/src/apply.ts`) that, in order:

1. Reads `branding.themeId` and calls
   `ThemeRegistry.apply(...)`.
2. Reads `branding.accent` and overrides `--primary` +
   `--ring` with the per-org / global accent (when
   non-null).
3. Reads `branding.customCss` and injects it as a
   `<style id="plexor-custom-css">` tag in `<head>`
   (idempotent: removes any prior tag with the same id
   before injecting).
4. Reads `branding.name` and `branding.logoUrl` and
   writes them to the `<title>` + `<link rel="icon">`
   (the document metadata is owned by the FE shell; this
   is the one place it reads from).

`applyBranding` SHALL be called BEFORE the React tree
mounts (in `web/apps/console/src/main.tsx`) so the first
paint is already branded — no flash of unstyled content.

### Requirement: BootConfig exposes the branding envelope

The backend SHALL extend the `GET /api/v1/boot` response
with a `branding` envelope:

```jsonc
{
  "branding": {
    "global": {
      "name": "Plexor",
      "logoUrl": "https://...",
      "accent": "#...",
      "customCss": ""
    },
    "theme": {
      "defaultPresetId": "paper",
      "availablePresets": ["paper", "midnight", "nord"]
    }
  }
}
```

The boot response SHALL include the global config even
when the operator hasn't touched it (the seeder writes a
baseline on first boot).

### Requirement: Custom CSS escape hatch

The FE SHALL apply operator-supplied `customCss` via
`applyBranding(branding)`. The CSS is injected as a
`<style>` tag into `<head>` and is capped at 16 KiB by
the backend (FluentValidation). Operators who try to
escape the scope are responsible for the result — the
v0.1 design system ships with sensible defaults that the
custom CSS layers on top of.

The custom CSS tag id `plexor-custom-css` is stable;
re-applying branding removes the prior tag before
injecting the new one.

### Requirement: AdminBrandingPage + LiveBrandPreview

The FE SHALL expose an `/admin/branding` route
(`AdminBrandingPage`) under
`web/apps/console/src/features/admin/branding/`:

- Form fields: `name`, `logoUrl`, `accent` (color
  picker), `themeId` (preset dropdown), `customCss`
  (textarea).
- `LiveBrandPreview` — applies the form values to a
  sample page (next to the form) WITHOUT persisting; uses
  `applyBranding` with a local in-memory
  `ResolvedBranding`.
- Submit calls `PUT /api/v1/branding/global` and shows
  success / error toasts.
- All labels are i18n-keyed (`branding.admin.*`).

The page is gated behind the `branding.update`
permission string; missing permission renders a 403
panel.

### Requirement: useInstalledThemes hook

The FE SHALL expose `useInstalledThemes(orgId)`
(`web/apps/console/src/features/admin/themes/hooks/useInstalledThemes.ts`)
with the following contract:

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

The hook SHALL persist state via the
`/api/v1/branding/themes/installations` API (the
backend replaces the localStorage MVP). The hook's
contract is unchanged from the localStorage MVP; only
the implementation swapped.

The hook SHALL be backed by `useSyncExternalStore` so
the marketplace UI reflects localStorage + API changes
from other tabs.

The hook SHALL silently ignore a stale `active` pointer
(theme was uninstalled): the hook returns
`active = null` and the FE falls back to
`bootConfig.branding.theme.defaultPresetId`.

### Requirement: ThemeMarketplacePage admin UI

The FE SHALL expose `/admin/themes/marketplace`
(`ThemeMarketplacePage`) under
`web/apps/console/src/features/admin/themes/`. The page
is gated on `branding.update`.

The page renders a grid of `CommunityTheme` cards (one
per registry entry) with `LiveBrandPreview`, a status
pill, and an action button. All labels are i18n-keyed
(`themes.marketplace.*`).

### Requirement: ThemeInstallation entity

The system SHALL expose a `ThemeInstallation` entity
(`Plexor.Modules.Branding.Domain.Entities.ThemeInstallation`,
schema `branding.theme_installations`) — one row per
`(orgId, themeId)` recording the install.

`ThemeInstallation` SHALL carry:

- `Id : Guid` — UUID v7 PK.
- `OrgId : Guid` — tenant scope.
- `ThemeId : string` — registry key.
- `Version : string` — semver at install time.
- `ManifestSignature : string` — HMAC-SHA256 hex of the
  canonical manifest.
- `ManifestPayload : string` (`jsonb`) — the canonical
  manifest bytes.
- `ActivatedAt : DateTimeOffset?` — null when installed
  but not active.
- `InstalledAt : DateTimeOffset`.
- `InstalledBy : Guid`.

`UNIQUE (OrgId, ThemeId)`. Partial index on
`(OrgId) WHERE ActivatedAt IS NOT NULL` enforces
"at most one active per org".

### Requirement: ThemeManifestVerifier

The system SHALL expose `ThemeManifestVerifier`
(`Plexor.Modules.Branding.Infrastructure.ThemeManifestVerifier`)
that verifies a manifest's HMAC-SHA256 signature against
the configured key.

Canonicalization rules:

- JSON serialized with deterministic key order (sorted
  alphabetically at every depth).
- No whitespace (compact JSON).
- The `tokens` object's keys are sorted too.

The verifier SHALL use a constant-time compare to prevent
timing attacks. A signature mismatch returns a stable
error code (`themes.signature.invalid`).

### Requirement: ThemeManifestSigningOptions

The system SHALL expose `ThemeManifestSigningOptions`
(`Plexor.Modules.Branding.Application.ThemeManifestSigningOptions`,
`SectionName = "Branding:Themes"`):

- `SigningKey : string` (env-bound via the shared env
  provider; never committed to the file).
- `KeyId : string` (default `"k1"`).

The options SHALL be bound + validated with
`ValidateDataAnnotations().ValidateOnStart()`. A missing
or empty `SigningKey` fails the host boot.

### Requirement: REST endpoints for theme installations

The system SHALL expose:

- `GET /api/v1/branding/themes/installations`
  (`branding.read`) — list installed themes for the
  caller's org.
- `POST /api/v1/branding/themes/installations`
  (`branding.update`) — install a theme. Body:
  `{ manifest, signature }`.
- `DELETE /api/v1/branding/themes/installations/{themeId}`
  (`branding.update`).
- `PUT /api/v1/branding/themes/installations/{themeId}/activate`
  (`branding.update`) — body:
  `{ themeId : string }` (the host loads the manifest
  from the registry, signs it internally if the theme is
  built-in, verifies for community themes).
- `DELETE /api/v1/branding/themes/installations/active`
  (`branding.update`) — clear the active theme.

Tenant-scoped: a user in Org X SHALL NOT view or mutate
Org Y's installations (HTTP 404, never 403).

### Requirement: Activation is atomic single-active

The activate handler SHALL atomically:

1. UPDATE the previous active row to set
   `ActivatedAt = NULL`.
2. UPDATE the new row to set `ActivatedAt = now()`.

Both UPDATEs run in a single DB transaction. The partial
unique index guards against application-layer bugs.

### Requirement: Audit emission (theme marketplace)

The `ThemeInstallationsController` SHALL emit:

- `POST .../installations` succeeds →
  `action = "theme.installed.installed"`,
  `payload_json = { "themeId": "<id>", "version": "..." }`.
- `PUT .../installations/{themeId}/activate` succeeds →
  `action = "theme.installed.activated"`,
  `payload_json = { "themeId": "<id>" }`.
- `DELETE .../installations/active` succeeds →
  `action = "theme.installed.deactivated"`,
  `payload_json = { "themeId": "<id>" }`.
- `DELETE .../installations/{themeId}` succeeds →
  `action = "theme.installed.uninstalled"`,
  `payload_json = { "themeId": "<id>" }`.

The plaintext manifest payload is NEVER included in
`payload_json`.

### Requirement: Permission strings

The system SHALL expose the following permission strings
in the role permission catalog:

- `branding.update` — granted to `admin` (built-in)
  by default.

The built-in `viewer` role has no branding permission
(read-only operators don't need to edit branding).

### Requirement: Migration order

`branding.*` migrations SHALL be applied after `realm`,
`sigil`, `atlas`, `quotas`, `storage`, and `network`
(FK in spirit — branding is a leaf concern). The
`Plexor.Migrator` orders schemas by FK dependency:
`realm` → `sigil` → `atlas` → `quotas` → `storage` →
`network` → `branding`. The `InitBranding` +
`InitThemeInstallations` migrations are the branding
migrations in v0.1.

## Key Entities

### `BrandingConfig`

`Plexor.Modules.Branding.Domain.Entities.BrandingConfig`
(schema `branding.branding_configs`). Fields as listed
above.

### `ThemeInstallation`

`Plexor.Modules.Branding.Domain.Entities.ThemeInstallation`
(schema `branding.theme_installations`). Fields as
listed above.

### `Theme` (FE)

`web/packages/tokens/src/themes/<id>.ts` — typed
init-only record:

```ts
export interface Theme {
  readonly id: string;
  readonly label: string;
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

### `CommunityTheme` (FE)

`web/packages/tokens/src/community/registry.ts` — typed
init-only record:

```ts
export interface CommunityTheme {
  readonly id: string;
  readonly label: string;
  readonly author: string;
  readonly version: string;
  readonly preview: { light: string; dark: string };
  readonly tokens: Theme;
}
```

### `ResolvedBranding` (FE)

The runtime shape consumed by `applyBranding`:

```ts
export interface ResolvedBranding {
  readonly themeId: string;
  readonly name: string;
  readonly logoUrl: string;
  readonly accent: string | null;
  readonly customCss: string;
}
```
