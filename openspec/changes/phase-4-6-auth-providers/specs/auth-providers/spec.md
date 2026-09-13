# Spec delta: auth-providers (phase-4-6-auth-providers)

This file is the proposed state of
`openspec/specs/auth-providers/spec.md` after the change
`openspec/changes/phase-4-6-auth-providers/` is merged. It
uses the `## ADDED Requirements` convention from OpenSpec —
every requirement here is a new requirement being introduced
by the change.

When the change lands, this delta is promoted into
`openspec/specs/auth-providers/spec.md` under `##
Requirements`, and the `## ADDED Requirements` heading is
removed.

## ADDED Requirements

### Requirement: `OrgAuthProviderConfig` aggregate

The system SHALL expose a per-organization authentication
provider configuration via the
`OrgAuthProviderConfig` entity
(`Plexor.Modules.Realm.Domain.Entities.OrgAuthProviderConfig`,
schema `realm`, table `org_auth_provider_configs`). One row
per org (UNIQUE on `OrgId`).

`OrgAuthProviderConfig` SHALL carry:

- `Id : Guid` — UUID v7 PK.
- `OrgId : Guid` — FK to `realm.organizations.id`.
  UNIQUE — exactly one config row per org.
- `Provider : OrgAuthProvider` — enum: `Sigil` (default;
  local email + password against `sigil.users.password_hash`)
  or `Oidc` (external IDP via OIDC).
- `OidcAuthority : string?` — OIDC issuer URL. Null when
  `Provider = Sigil`.
- `OidcClientId : string?` — confidential client id. Null
  when `Provider = Sigil`.
- `OidcClientSecretProtected : string?` — encrypted (via
  `IDataProtector`) OIDC confidential client secret. Null
  when `Provider = Sigil`.
- `OidcScopes : IReadOnlyList<string>` — default
  `["openid", "profile", "email"]`. Stored as Postgres
  `text[]`. Admin can extend via PUT.
- `CreatedAt : DateTimeOffset` / `UpdatedAt :
  DateTimeOffset` — UTC.

Invariants:

- Exactly **one** row per `OrgId` (UNIQUE).
- `OidcAuthority`, `OidcClientId`, and
  `OidcClientSecretProtected` SHALL all be non-null iff
  `Provider = Oidc`; all three SHALL be null when
  `Provider = Sigil`. The PUT endpoint enforces this at the
  boundary.
- `OidcAuthority` SHALL be a URL — must be absolute, must be
  HTTPS in production. The FluentValidation chain rejects
  non-HTTPS values with HTTP 400.
- `OidcClientSecretProtected` SHALL be the encrypted
  ciphertext; the plaintext is never persisted. The
  purpose-bound `IDataProtector` uses purpose string
  `OrgAuthProviderConfig.OidcClientSecret` so a
  different-purpose protector elsewhere cannot decrypt the
  same ciphertext.

### Requirement: First-boot seeder

The system SHALL seed a `Sigil` row in
`realm.org_auth_provider_configs` for every existing
organization on first boot via
`OrgAuthProviderSeeder` (an `IHostedService` in
`Plexor.Modules.Realm.Application.AuthProviders`). The
seeder is idempotent — re-runs against an already-seeded
fleet are no-ops.

The seeder SHALL run in both `Plexor.Host` (every restart)
and `Plexor.Migrator` (first deploy). Wired via the
`AddRealmAuthProviders()` installer extension on each
composition root.

### Requirement: Encryption-at-rest via `IDataProtector`

The OIDC client secret SHALL be encrypted at rest via
`Microsoft.AspNetCore.DataProtection.IDataProtector` before
the row hits disk. The host's
`AddDataProtection().PersistKeysToFileSystem(...)` registers
the keyring under the OS-conventional Plexor data root
(`<LocalAppData|XdgDataHome|ApplicationSupport>/plexor/dataprotection-keys`).

The controller SHALL mint a purpose-bound protector at the
call site via
`dataProtectionProvider.CreateProtector("OrgAuthProviderConfig.OidcClientSecret")`.
A different-purpose protector elsewhere cannot decrypt the
same ciphertext — defense in depth against a misconfigured
DI registration that mints a "default" protector for the
whole app.

The decrypted plaintext SHALL only be held in memory for the
duration of the HTTP call that needs it (the `POST .../test`
endpoint decrypts locally for an outbound OIDC discovery
fetch). It SHALL NEVER be logged, NEVER returned in the
response, NEVER persisted in plaintext.

### Requirement: REST endpoints for org admin

The system SHALL expose the following endpoints, all behind
`Plexor.Shared.Authorization.RequirePermissionAttribute`:

- `GET /api/v1/iam/orgs/{orgId}/auth-provider`
  (`org.auth.read`) — fetch the current config. OIDC
  fields are redacted; `OidcClientSecretMasked` carries
  the literal string `"***"` when the persisted row has a
  non-null ciphertext, `null` otherwise.
- `PUT /api/v1/iam/orgs/{orgId}/auth-provider`
  (`org.auth.update`) — upsert (switch `Sigil` ↔ `Oidc`,
  rotate OIDC fields). Validated by FluentValidation:
  - `Provider` is non-empty AND one of `sigil` / `oidc`
    (case-insensitive).
  - When `Provider = oidc`: `OidcAuthority` is non-empty AND
    an absolute HTTPS URL; `OidcClientId` is non-empty AND
    ≤ 256 chars; `OidcClientSecret` (when supplied) is
    ≥ 8 chars.
  - When `OidcScopes` is supplied: every entry is non-empty.
- `POST /api/v1/iam/orgs/{orgId}/auth-provider/test`
  (`org.auth.update`) — run the OIDC discovery-document
  fetch against the configured authority. The endpoint
  decrypts the stored ciphertext locally for the outbound
  call and returns `{ connected, discoveryDocumentUrl,
  availableScopes, error }`. The plaintext secret is never
  returned.

Tenant-scoped: a user authenticated in Org X SHALL NOT view
or mutate Org Y's row. The controller SHALL return HTTP 404
(not 403 — never leak existence) when the URL `orgId`
doesn't match `currentUser.TenantId`.

### Requirement: Permission strings

The system SHALL expose two new permission strings in the
role permission catalog
(`Plexor.Modules.Sigil.Domain.Entities.Role.Permissions`):

- `org.auth.read` — `AuthProviderPermissions.Read`.
- `org.auth.update` — `AuthProviderPermissions.Update`.

The built-in `admin` role carries the `*` wildcard and
already covers both strings (the wildcard is minted by
`Plexor.Migrator/IdentityBootstrapper`); no seeder change
required. Per-role grants (without wildcard) can be added
via the existing role-assignment endpoints.

### Requirement: Standard 401 / 403 / 404 contract

The system SHALL apply the standard authentication /
authorization / tenant-isolation contract on every endpoint
in this capability, matching every other Plexor endpoint:

- **No bearer / invalid bearer** → HTTP 401 with the
  standard `identity.token.*` code from the Sigil
  capability.
- **Valid bearer, missing permission** → HTTP 403 with
  `code = "identity.permission.denied"`.
- **Valid bearer, valid permission, wrong tenant** → HTTP
  404 (the URL `orgId` doesn't match `currentUser.TenantId`).

The `code` value SHALL be drawn from the identity capability's
stable-code namespace. Clients MUST branch on `code`, never
on `message`.

### Requirement: Migration order

`realm.org_auth_provider_configs` migrations SHALL be applied
after `realm.organizations.id` exists (FK in spirit — the
seeder enumerates orgs) and before any other module's
migrations that depend on the per-tenant auth-provider
config (4.6.2 / 4.6.3 will land such migrations). The
`Plexor.Migrator` orders schemas by FK dependency: `realm` →
`sigil` → `atlas` → (future) `ledger`, `forge`, `outpost`,
`shard`. The `InitAuthProviders` migration is the second
Realm migration (the first is `20260718143631__Initial`).

## ADDED Key Entities

### `OrgAuthProviderConfig`

`Plexor.Modules.Realm.Domain.Entities.OrgAuthProviderConfig`
(schema `realm.org_auth_provider_configs`). Fields as
listed above. `UNIQUE (org_id)`.

### `OrgAuthProvider` enum

`Plexor.Modules.Realm.Domain.Entities.OrgAuthProvider`
(PascalCase, bound via the `JsonStringEnumConverter`
registered in `Plexor.Host/Program.cs`). Members:

- `Sigil = 0` — local email + password against
  `sigil.users.password_hash`.
- `Oidc = 1` — external OIDC; Plexor validates the
  IDP-issued JWT against the per-tenant config.

### `AuthProviderPermissions`

`Plexor.Shared.Kernel.AuthProviders.AuthProviderPermissions`
mirrors `Plexor.Shared.Kernel.Quotas.QuotaPermissions`.
Stable permission strings: `Read = "org.auth.read"`,
`Update = "org.auth.update"`, `AdminWildcard = "*"`.
