# Design: theme-marketplace-backend

Technical decisions for the theme marketplace backend
persistence + HMAC verification.

## Why HMAC, not public-key

The manifest signature protects two things:

1. **Authenticity.** A theme installed in Plexor really
   came from the author whose name is on it.
2. **Tamper-evidence.** The token set wasn't modified in
   transit.

Three options:

1. **HMAC-SHA256 (symmetric).** Chosen. The author and
   the host share a key. The author runs the same
   `manifest-signing` CLI as the host; the host verifies
   the same key.
2. **RSA / Ed25519 (asymmetric).** Considered, rejected
   for v0.1 — the marketplace is Plexor-curated; key
   distribution is straightforward with a shared secret.
3. **No signature.** Rejected — the marketplace can't
   accept community themes without verification.

HMAC-SHA256 is the right v0.1 trade-off. A future
multi-author marketplace (Phase 5+) can adopt Ed25519
per-author without breaking the install surface.

## Canonical manifest format

The bytes that are signed:

```jsonc
{
  "id": "midnight-violet",
  "version": "1.0.0",
  "author": "Plexor Community",
  "tokens": {
    "Background": "oklch(0.18 0.02 270)",
    "Surface1": "oklch(0.22 0.02 270)",
    "Primary": "oklch(0.55 0.18 280)",
    // ... full OKLCH token set
  }
}
```

Canonicalization rules:

- JSON serialized with deterministic key order (sorted
  alphabetically at every depth).
- No whitespace (compact JSON).
- The `tokens` object's keys are sorted too.

The author signs the canonical bytes; the host
re-canonicalizes the incoming manifest and computes the
same HMAC. Any drift (different key order, whitespace,
trailing newline) fails the signature check.

## Config — signing key

```toml
[branding.themes]
signing_key = "env:THEMES_SIGNING_KEY"
key_id = "k1"
```

`env:THEMES_SIGNING_KEY` is resolved at startup via the
shared env-prefix env provider (per
`configuration-toml-env.md`). The key itself never lands
in the file. The host rejects a missing or empty key at
startup (`ValidateOnStart`).

A future multi-key verification
(`signing_key_v2 = "env:THEMES_SIGNING_KEY_V2"`) lands in
Phase 5+ alongside key rotation.

## Why a `manifest_payload` column

The `ThemeInstallation.manifest_payload` column stores
the canonical manifest as `jsonb`. Why store it (vs
verifying + dropping)?

- **Audit trail.** The audit log records what was
  installed when. Without the manifest, the audit can
  say "midnight-violet was activated" but not "with
  what tokens".
- **Rollback.** A future "rollback to a previous
  installation" feature reads the stored manifest
  directly.
- **Signature stability.** The signature is over the
  manifest bytes; storing the bytes + signature together
  lets a future "re-verify" tool catch drift.

The column is `jsonb` (binary JSON), not `text` — the
canonical manifest round-trips through Postgres's JSON
parser without reformatting drift.

## Activation — atomic single-active

A theme becomes active via
`PUT .../installations/{themeId}/activate`. The handler:

1. Verifies the theme is installed (the row exists).
2. UPDATE the previous active row to set
   `activated_at = NULL`.
3. UPDATE the new row to set `activated_at = now()`.

Both UPDATEs run in a single transaction. The
`UNIQUE` constraint + the partial index
`(org_id) WHERE activated_at IS NOT NULL` (added in the
migration) enforces "at most one active per org" at the
DB level.

(Phase 5+ adds a partial UNIQUE index for the constraint;
v0.1 relies on the application's atomic UPDATE.)

## Audit payload

The audit events carry the minimum the auditor needs to
reconstruct the timeline:

```jsonc
// theme.installed.installed
{ "themeId": "midnight-violet", "version": "1.0.0" }

// theme.installed.activated
{ "themeId": "midnight-violet" }

// theme.installed.deactivated
{ "themeId": "midnight-violet" }

// theme.installed.uninstalled
{ "themeId": "midnight-violet" }
```

The full token set is in `atlas.audit_entries.payload_json`
only when the operator chooses to attach it (Phase 5+).

## FE migration — localStorage → API

The `useInstalledThemes` hook's localStorage implementation
is replaced with API calls. The hook contract is unchanged:

```ts
// same shape as before; the implementation now hits the API
export function useInstalledThemes(orgId: string): {
  installed: CommunityTheme[];
  active: string | null;
  install: (theme: CommunityTheme) => void;
  uninstall: (themeId: string) => void;
  activate: (themeId: string) => void;
  deactivate: () => void;
};
```

The kubb-generated hooks (`useBrandingThemesInstallations`,
`useActivateBrandingThemesInstallationByThemeId`, etc.)
back the implementation. Optimistic updates keep the UI
snappy; a mutation failure rolls back the optimistic
state and surfaces the error.

## Open questions deferred

- **Remote registry / download path.** Community theme
  authors publish a manifest + signed payload somewhere;
  Plexor fetches it. The MVP requires the operator to
  supply the manifest + signature in the POST body.
- **Multi-key signing.** Phase 5+.
- **Theme marketplace UI for non-admin users.** Phase 5+.
- **Theme auto-update.** Phase 5+.
- **Theme preview without install.** Phase 5+.
