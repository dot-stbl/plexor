# Change: phase-4-6-auth-providers

## Why

Plexor currently ships a single identity backend — the Sigil
module's email + password + JWT path (see
`openspec/specs/identity/spec.md`). A SaaS deploy with
enterprise tenants needs the option to point a tenant at an
external OIDC provider (Keycloak, Authentik, Dex, Google
Workspace, Azure AD, etc.) instead of maintaining local
credentials. This change ships the **configuration layer** for
per-tenant authentication provider selection.

The auth-providers capability is a single-provider-per-tenant
model (no `SigIL AND Oidc` for the same tenant in v0.1 — see
plan §"Aggregate shape"). The two backends are never mixed
per request. The Sigil backend remains the local break-glass
admin path even when the tenant has switched to OIDC.

## What

A new row per org in `realm.org_auth_provider_configs`
describing which backend serves logins for that tenant. v1
ships the schema, the first-boot seeder, the encryption-at-rest
plumbing for the OIDC client secret, and the three org-admin
REST endpoints (`GET` / `PUT` / `POST test`) on
`/api/v1/iam/orgs/{orgId}/auth-provider`. The actual
authentication flow (`IAuthProvider` interface + Sigil + OIDC
implementations + OIDC flow endpoints `/auth/oidc/authorize`
+ `/callback` + `/logout`) lands in follow-up commits
(4.6.2 / 4.6.3).

### Schema

`realm.org_auth_provider_configs` (new table, schema
`realm`, owner `Plexor.Modules.Realm`):

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | UUID v7 PK |
| `org_id` | `uuid` | UNIQUE — exactly one row per org |
| `provider` | `int` | enum: `0` = Sigil, `1` = Oidc |
| `oidc_authority` | `varchar(2048)` (NULL) | issuer URL |
| `oidc_client_id` | `varchar(256)` (NULL) | confidential client id |
| `oidc_client_secret_protected` | `varchar(4096)` (NULL) | `IDataProtector.Protect(...)` ciphertext |
| `oidc_scopes` | `text[]` (NOT NULL) | default `["openid", "profile", "email"]` |
| `created_at`, `updated_at` | `timestamptz` | UTC |

`UNIQUE (org_id)` enforces the "exactly one row per org"
invariant. The Migrator's `OrgAuthProviderSeeder` ensures
every existing org has a Sigil row on first boot; re-runs are
no-ops.

### Encryption-at-rest

The OIDC client secret is encrypted via
`Microsoft.AspNetCore.DataProtection.IDataProtector` before
the row hits disk. The purpose string is
`OrgAuthProviderConfig.OidcClientSecret` so a different-purpose
protector elsewhere can't decrypt the same ciphertext. The
keyring lives on disk under
`<data-root>/dataprotection-keys/`. The GET response carries
`OidcClientSecretMasked = "***"` — the plaintext is never
returned through the REST surface.

### REST endpoints

All gated by `[RequirePermission]`:

- `GET /api/v1/iam/orgs/{orgId}/auth-provider`
  (`org.auth.read`) — fetch the current config.
- `PUT /api/v1/iam/orgs/{orgId}/auth-provider`
  (`org.auth.update`) — upsert (switch Sigil ↔ Oidc +
  rotate OIDC fields).
- `POST /api/v1/iam/orgs/{orgId}/auth-provider/test`
  (`org.auth.update`) — run the OIDC discovery-document
  fetch against the configured authority.

Tenant-scoped: a user in Org X SHALL NOT view or mutate Org
Y's row (404 when the URL `orgId` doesn't match
`currentUser.TenantId`).

### First-boot seeder

`Plexor.Modules.Realm.Application.AuthProviders.OrgAuthProviderSeeder`
— runs on every host / migrator startup. Idempotent: orgs
that already have a config row are skipped. Wired via
`AddRealmAuthProviders()` in both `Plexor.Host/Program.cs` and
`Plexor.Migrator/Program.cs`.

### Permission strings

`org.auth.read`, `org.auth.update` — added to
`Plexor.Shared.Kernel.AuthProviders.AuthProviderPermissions`
mirroring the `QuotaPermissions` pattern. The built-in
`admin` role already carries the `*` wildcard so it covers
both strings; no seeder change required.

## Impact

- **`realm` capability** — schema + a new per-tenant config
  row. No change to existing `Organization`, `Team`, or
  `Folder` entities.
- **`identity` capability** — two new permission strings
  (`org.auth.read`, `org.auth.update`). The built-in
  `admin` role's `*` wildcard already covers them; no
  seed change. The additive delta is in
  `openspec/changes/phase-4-6-auth-providers/specs/identity/spec.md`.
- **`Plexor.Host`** — adds `OrgAuthProvidersController` (in
  this assembly's `Controllers/` folder — the Realm module
  has no Api project yet; creating one for a 3-endpoint
  surface is a refactor deferred per the user), wires
  `AddDataProtection().PersistKeysToFileSystem(...)`, the
  FluentValidation validator, and the OIDC discovery
  `IHttpClientFactory` named client.
- **`Plexor.Migrator`** — calls `AddRealmAuthProviders()` so
  the Migrator's first-boot applies the seed right after the
  migration runs.
- **`Plexor.Shared.Kernel`** — adds
  `AuthProviders/AuthProviderPermissions.cs` with the two
  permission strings.
- **`Plexor.Shared.Persistence`** — adds
  `Tables.OrgAuthProviderConfigs = "org_auth_provider_configs"`.

## Out of scope (lands in 4.6.2 / 4.6.3 or later)

- **`IAuthProvider` interface + `AuthResolution` discriminated
  result** — the abstraction the bearer handler routes through.
- **`SigilAuthProvider` implementation** — wraps the existing
  `IPasswordHasher` + `LoginCommand` paths.
- **`ExternalOidcAuthProvider` implementation** — JWKS fetch
  + cache + `TokenValidationParameters` configured from the
  per-tenant config.
- **`IAuthProviderResolver`** dispatcher; bearer handler
  consults the resolver instead of the Sigil-only path.
- **OIDC flow endpoints** (`/auth/oidc/authorize`,
  `/callback`, `/logout`).
- **`/auth/login` / `/auth/refresh` updates** — guard on the
  tenant's `OrgAuthProvider`, redirect to OIDC if not Sigil.
- **Multi-IDP per tenant** (single provider in v0.1).
- **SCIM-based OIDC user provisioning**.
- **Per-org certificate pinning for the OIDC authority**.
