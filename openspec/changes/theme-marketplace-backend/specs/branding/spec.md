# Spec delta: branding (theme-marketplace-backend)

This file is the additive delta for the branding capability
introduced by the change
`openspec/changes/theme-marketplace-backend/`. Existing
branding Requirements are unchanged — only new requirements
are added.

When the change lands, this delta is promoted into
`openspec/specs/branding/spec.md` under `## Requirements`,
and the `## ADDED Requirements` heading is removed.

## ADDED Requirements

### Requirement: ThemeInstallation entity

The system SHALL expose a `ThemeInstallation` entity
(`Plexor.Modules.Branding.Domain.Entities.ThemeInstallation`,
schema `branding.theme_installations`) — one row per
`(orgId, themeId)` recording the install.

`ThemeInstallation` SHALL carry:

- `Id : Guid` — UUID v7 PK.
- `OrgId : Guid` — tenant scope.
- `ThemeId : string` — registry key (`midnight-violet`).
- `Version : string` — semver at install time
  (`1.0.0`).
- `ManifestSignature : string` — HMAC-SHA256 hex of the
  canonical manifest.
- `ManifestPayload : string` (`jsonb`) — the canonical
  manifest bytes (id + version + author + tokens).
- `ActivatedAt : DateTimeOffset?` — null when the theme
  is installed but not active; non-null when it is the
  org's active theme.
- `InstalledAt : DateTimeOffset` — UTC.
- `InstalledBy : Guid` — actor user id.

`UNIQUE (OrgId, ThemeId)`. Partial index on
`(OrgId) WHERE ActivatedAt IS NOT NULL` (added in the
`InitThemeInstallations` migration) enforces "at most one
active per org" at the DB level.

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

### Requirement: REST endpoints

The system SHALL expose the following endpoints, all
behind `Plexor.Shared.Authorization.RequirePermissionAttribute`:

- `GET /api/v1/branding/themes/installations`
  (`branding.read`) — list installed themes for the
  caller's org.
- `POST /api/v1/branding/themes/installations`
  (`branding.update`) — install a theme. Body:
  `{ manifest : ManifestPayload,
  signature : string }`. The host verifies the signature
  before persisting.
- `DELETE /api/v1/branding/themes/installations/{themeId}`
  (`branding.update`) — uninstall.
- `PUT /api/v1/branding/themes/installations/{themeId}/activate`
  (`branding.update`) — body:
  `{ themeId : string }` (the host loads the manifest
  from the registry, signs it internally if the theme is
  built-in, verifies for community themes).
- `DELETE /api/v1/branding/themes/installations/active`
  (`branding.update`) — clear the active theme.

Tenant-scoped: a user in Org X SHALL NOT view or mutate
Org Y's installations (HTTP 404, never 403 — same
contract as every other Plexor endpoint).

### Requirement: Activation is atomic single-active

The activate handler SHALL atomically:

1. UPDATE the previous active row to set
   `ActivatedAt = NULL`.
2. UPDATE the new row to set `ActivatedAt = now()`.

Both UPDATEs run in a single DB transaction. A partial
unique index on `(OrgId) WHERE ActivatedAt IS NOT NULL`
guards against the application layer forgetting to clear
the previous active row.

### Requirement: Audit emission (4 events)

The `ThemeInstallationsController` SHALL emit the
following audit events, each with a stable dot.case wire
name:

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
`payload_json` — operators query
`GET /api/v1/branding/themes/installations` to retrieve
the full manifest.

### Requirement: Migration order

`branding.theme_installations` migrations SHALL be applied
after `realm`, `sigil`, `atlas` (FK in spirit — actor
trace), and `quotas` (FK in spirit — quota tracking on
branding mutations). The `Plexor.Migrator` orders schemas
by FK dependency:
`realm` → `sigil` → `atlas` → `quotas` → `storage` →
`network` → `branding`. The `InitThemeInstallations`
migration is appended to the existing branding migrations.

### Requirement: Frontend hook contract unchanged

The FE's `useInstalledThemes(orgId)` hook contract
(`installed`, `active`, `install`, `uninstall`,
`activate`, `deactivate`) SHALL be unchanged from the
localStorage MVP. The backend change replaces the
localStorage implementation with API calls
(`kubb`-generated hooks); the hook's surface is stable.
