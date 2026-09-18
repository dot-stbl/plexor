# Change: theme-marketplace-backend

## Why

The FE `theme-marketplace` change ships the registry + admin
UI with localStorage persistence. That's a valid MVP for
single-tenant self-hosted deploys but breaks down in
multi-tenant SaaS: a tenant's theme choice doesn't
survive a browser switch, the choice isn't visible across
admins, and there's no audit trail of who installed what
when.

This change ships the backend persistence layer:

1. A `ThemeInstallation` entity — one row per
   `(orgId, themeId)` recording the install.
2. REST endpoints under
   `/api/v1/branding/themes/installations` — list,
   install, uninstall, activate, deactivate.
3. HMAC-signed manifest verification — a theme manifest
   carries `id`, `version`, `author`, `tokens`, and an
   HMAC-SHA256 signature the host verifies before
   activation. This is the security boundary that lets
   Plexor accept community themes without trusting them
   blindly.
4. Audit emission for `theme.installed.activated` /
   `theme.installed.deactivated` (the events the FE MVP
   deferred).

## What

### Schema

New table `branding.theme_installations` (schema
`branding`, owner `Plexor.Modules.Branding`):

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | UUID v7 PK |
| `org_id` | `uuid` | tenant scope |
| `theme_id` | `varchar(128)` | registry key |
| `version` | `varchar(32)` | semver at install time |
| `manifest_signature` | `varchar(256)` | HMAC-SHA256 hex of the canonical manifest |
| `manifest_payload` | `jsonb` | the canonical manifest (id + version + author + tokens) |
| `activated_at` | `timestamptz` (NULL) | when the theme became the active theme for the org |
| `installed_at` | `timestamptz` | UTC |
| `installed_by` | `uuid` | actor user id |

`UNIQUE (org_id, theme_id)`. Index on `(org_id,
activated_at DESC)` for "what's active per org" queries.

### REST endpoints

- `GET /api/v1/branding/themes/installations`
  (`branding.read`) — list installed themes for the
  caller's org.
- `POST /api/v1/branding/themes/installations`
  (`branding.update`) — install a theme. Body:
  `{ manifest: { id, version, author, tokens },
  signature }`. The host verifies the signature against
  the configured public key (HMAC-SHA256); a mismatch
  returns HTTP 400 with `code = "themes.signature.invalid"`.
- `DELETE /api/v1/branding/themes/installations/{themeId}`
  (`branding.update`) — uninstall.
- `PUT /api/v1/branding/themes/installations/{themeId}/activate`
  (`branding.update`) — mark the theme active for the
  org. Atomically clears the previous active row.
- `DELETE /api/v1/branding/themes/installations/active`
  (`branding.update`) — clear the active theme (fall back
  to the org's `branding.theme.defaultPresetId`).

### HMAC verification

`Plexor.Modules.Branding.Infrastructure.ThemeManifestVerifier`
— verifies the manifest's HMAC-SHA256 signature against
the configured key. The canonical manifest (the bytes that
were signed) is:

```jsonc
{
  "id": "<themeId>",
  "version": "<semver>",
  "author": "<author>",
  "tokens": { ... OKLCH token set ... }
}
```

The key is configured via
`[Branding:Themes] SigningKey` (env-bound, not committed):

```toml
[branding.themes]
signing_key = "env:THEMES_SIGNING_KEY"   # resolved at startup
key_id = "k1"
```

`ValidateOnStart()` rejects a missing or empty key.
A signing-key rotation lands in Phase 5+ (multi-key
verification).

### Audit emission

`ThemesController` emits:

- `POST .../installations` succeeds →
  `action = "theme.installed.installed"` with
  `payload_json = { "themeId": "<id>", "version": "..." }`.
- `PUT .../installations/{themeId}/activate` succeeds →
  `action = "theme.installed.activated"`.
- `DELETE .../installations/active` succeeds →
  `action = "theme.installed.deactivated"`.
- `DELETE .../installations/{themeId}` succeeds →
  `action = "theme.installed.uninstalled"`.

The four wire names join the existing
`org.auth_provider.changed` namespace pattern in the audit
log.

### PUT simplification

The original `PUT .../themes/{themeId}/activate` accepted
the full theme manifest in the body. The follow-up commit
(`adf2ce6`) simplified it: the body is now
`{ themeId : string }` only — the host loads the
manifest from the registry, signs it internally if the
theme is built-in, and verifies it against the configured
key for community themes. The PUT surface becomes
"operator says 'use this theme'" without exposing the
crypto plumbing.

## Impact

- **`branding` capability** — additive delta:
  - `ThemeInstallation` entity.
  - 5 REST endpoints.
  - HMAC signature verification.
  - 4 audit events.
  - PUT body simplification.
  See `openspec/changes/theme-marketplace-backend/specs/branding/spec.md`.
- **`Plexor.Modules.Branding`** — adds the
  `ThemeInstallationsController` + the
  `ThemeManifestVerifier` infrastructure.

## Out of scope

- **Remote registry / download path** — community theme
  authors publish a manifest + signed payload somewhere;
  Plexor fetches it. The MVP requires the operator to
  supply the manifest + signature in the POST body.
  Phase 5+ adds a remote-registry URL + signed fetch.
- **Multi-key signing** — Phase 5+ adds key rotation +
  multi-key verification.
- **Theme marketplace UI for non-admin users** — the
  marketplace stays admin-gated in v0.1.
- **Theme auto-update** — when a theme author publishes a
  new version, the host doesn't auto-install it. The
  operator chooses when to upgrade.
- **Theme preview without install** — Phase 5+.
