# Capability: auth-providers

## Purpose

Per-tenant authentication backend selection. The Sigil identity
provider (`openspec/specs/identity/spec.md`) ships email + password
+ JWT as the default for every organization. A multi-tenant SaaS
deploy with enterprise tenants needs the option to point a
tenant at an external OIDC identity provider (Keycloak, Authentik,
Azure AD, Google Workspace, etc.) instead of maintaining local
credentials.

This capability models the per-tenant choice between "local
Sigil" and "external OIDC". The two backends are **never mixed
per request**: a tenant is either Sigil-only (no external IDP
configured) or OIDC-only (an external IDP is configured and
Sigil remains the local break-glass admin path).

`v0.1` ships the Sigil provider plus the per-tenant OIDC
configuration layer; the OIDC flow endpoints and inbound JWT
validation also ship. SCIM-based user provisioning lands in
Phase 5+.

## Requirements

### Requirement: `OrgAuthProviderConfig` aggregate

The system SHALL expose a per-organization authentication
provider configuration via the
`OrgAuthProviderConfig` entity
(`Plexor.Modules.Realm.Domain.Entities.OrgAuthProviderConfig`,
schema `realm`, table `org_auth_provider_configs`). One row per
org (UNIQUE on `OrgId`).

`OrgAuthProviderConfig` SHALL carry:

- `Id : Guid` — UUID v7 PK.
- `OrgId : Guid` — FK to `realm.organizations.id`. UNIQUE —
  exactly one config row per org.
- `Provider : OrgAuthProvider` — enum: `Sigil` (default; local
  email + password against `sigil.users.password_hash`) or
  `Oidc` (external IDP via OIDC).
- `OidcAuthority : string?` — OIDC issuer URL. Null when
  `Provider = Sigil`.
- `OidcClientId : string?` — confidential client id. Null when
  `Provider = Sigil`.
- `OidcClientSecretProtected : string?` — encrypted (via
  `IDataProtector`) OIDC confidential client secret. Null when
  `Provider = Sigil`.
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
organization on first boot via `OrgAuthProviderSeeder` (an
`IHostedService` in
`Plexor.Modules.Realm.Application.AuthProviders`). The seeder
is idempotent — re-runs against an already-seeded fleet are
no-ops.

The seeder SHALL run in both `Plexor.Host` (every restart) and
`Plexor.Migrator` (first deploy). Wired via the
`AddRealmAuthProviders()` installer extension on each
composition root.

### Requirement: Encryption-at-rest via `IDataProtector`

The OIDC client secret SHALL be encrypted at rest via
`Microsoft.AspNetCore.DataProtection.IDataProtector` before the
row hits disk. The host's
`AddDataProtection().PersistKeysToFileSystem(...)` registers
the keyring under the OS-conventional Plexor data root
(`<LocalAppData|XdgDataHome|ApplicationSupport>/plexor/dataprotection-keys`).

The `OrgAuthProviderSecretProtector` (in
`Plexor.Modules.Realm.Infrastructure.AuthProviders`) mints a
purpose-bound protector at construction time via
`dataProtectionProvider.CreateProtector("OrgAuthProviderConfig.OidcClientSecret")`.
A different-purpose protector elsewhere cannot decrypt the
same ciphertext — defense in depth against a misconfigured DI
registration that mints a "default" protector for the whole app.

The decrypted plaintext SHALL only be held in memory for the
duration of the HTTP call that needs it (the OIDC flow
endpoints and `POST .../test` endpoint decrypt locally for
outbound calls). It SHALL NEVER be logged, NEVER returned in
the response, NEVER persisted in plaintext.

### Requirement: `IAuthProvider` abstraction

The system SHALL expose `IAuthProvider`
(`Plexor.Modules.Sigil.Application.AuthProviders.IAuthProvider`)
with two implementations in v0.1:

- `SigilAuthProvider` — wraps the existing
  `IJwtSigningService.VerifyAsync` path. Gates on the tenant's
  `OrgAuthProviderConfig` (returns null if the tenant is Oidc).
- `ExternalOidcAuthProvider` — fetches the IDP's JWKS via
  `IJwksFetcher` (cached for 1h in `IMemoryCache`), validates
  the JWT against the per-tenant `TokenValidationParameters`
  (`ValidIssuer = OidcAuthority`,
  `ValidAudience = OidcClientId`), maps external role claims
  (`realm_access.roles[]` for Keycloak, `roles[]` generic) to
  Plexor `Role.Name` values via `IRoleResolver`.

`IAuthProviderResolver` (`Plexor.Modules.Sigil.Application.AuthProviders.IAuthProviderResolver`)
dispatches a raw bearer credential to the right provider based
on the credential's `iss` claim. It caches the `(iss →
providerId)` mapping in `IMemoryCache` for 5 minutes (Phase 5+
adds explicit cache-bust on `PUT /api/v1/iam/orgs/{orgId}/auth-provider`).

`BearerAuthenticationHandler` calls the resolver; it no longer
verifies JWTs directly. The API-key path (`kid_xxx.<secret>`)
remains unchanged.

### Requirement: OIDC flow endpoints

The system SHALL expose the three endpoints that operators hit
when logging into a tenant configured for external OIDC:

- `GET /auth/oidc/authorize?org={orgId}&redirect={path}` —
  generates a PKCE pair (RFC 7636, S256) + opaque state token,
  persists the context in `IOidcFlowStateStore` (TTL 10
  minutes, one-shot consumption), and 302-redirects to the IDP's
  authorization endpoint:
  ```
  {OidcAuthority}/protocol/openid-connect/auth
    ?response_type=code
    &client_id={OidcClientId}
    &redirect_uri={callback_url}
    &scope=openid+profile+email
    &state={state}
    &code_challenge={challenge}
    &code_challenge_method=S256
  ```
- `GET /auth/oidc/callback?code=...&state=...` — validates
  state via `IOidcFlowStateStore.Consume`, exchanges the code
  via `IOidcTokenClient.ExchangeCodeAsync` (with basic auth
  on the outbound call using the decrypted client secret),
  validates the returned `id_token` via `IOidcIdTokenValidator`,
  finds-or-creates the Plexor `User` row via
  `IOidcUserProvisioner` (with a `viewer` RoleBinding for
  freshly-onboarded users), mints a Plexor bearer via
  `ITokenIssuer`, and 302-redirects to
  `{OriginalRedirect}?access_token={bearer}`.
- `POST /auth/oidc/logout` — best-effort 204 in v0.1 (no
  Plexor-side cookie to clear; RP-initiated call to
  `end_session_endpoint` lands in Phase 5+).

All three endpoints are anonymous (the auth middleware returns
401 for unauthenticated requests; these run before that).

### Requirement: REST endpoints for org admin

The system SHALL expose the following endpoints, all behind
`Plexor.Shared.Authorization.RequirePermissionAttribute`:

- `GET /api/v1/iam/orgs/{orgId}/auth-provider`
  (`org.auth.read`) — fetch the current config. OIDC
  fields are redacted; `OidcClientSecretMasked` carries the
  literal string `"***"` when the persisted row has a non-null
  ciphertext, `null` otherwise.
- `PUT /api/v1/iam/orgs/{orgId}/auth-provider`
  (`org.auth.update`) — upsert (switch `Sigil` ↔ `Oidc`,
  rotate OIDC fields). Validated by FluentValidation:
  - `Provider` is non-empty AND one of `sigil` / `oidc`
    (case-insensitive).
  - When `Provider = oidc`: `OidcAuthority` is non-empty AND
    an absolute HTTPS URL; `OidcClientId` is non-empty AND
    ≤ 256 chars; `OidcClientSecret` (when supplied) is ≥ 8
    chars.
  - When `OidcScopes` is supplied: every entry is non-empty.
- `POST /api/v1/iam/orgs/{orgId}/auth-provider/test`
  (`org.auth.update`) — run the OIDC discovery-document fetch
  against the configured authority. The endpoint decrypts the
  stored ciphertext locally for the outbound call and returns
  `{ connected, discoveryDocumentUrl, availableScopes, error }`.
  The plaintext secret is never returned.

Tenant-scoped: a user authenticated in Org X SHALL NOT view or
mutate Org Y's row. The controller SHALL return HTTP 404 (not
403 — never leak existence) when the URL `orgId` doesn't
match `currentUser.TenantId`.

### Requirement: `POST /auth/login` IDP guard

`POST /auth/login` (existing endpoint) SHALL look up the
target org's `OrgAuthProviderConfig` and branch on the
provider:

- `Provider == Sigil` (or config row missing — backward-compat
  with single-tenant v0.1) → existing email + password flow
  unchanged.
- `Provider == Oidc` → HTTP 400 with
  `code = "identity.credentials.provider_mismatch"` and body
  `{ redirect: "/auth/oidc/authorize?org={orgId}&redirect={path}" }`.
  The console's login screen reads this `redirect` and
  navigates the operator to the OIDC flow.

### Requirement: `POST /auth/refresh` iss-binding

`POST /auth/refresh` SHALL peek the supplied refresh token's
`iss` claim (no signature check) and branch:

- `iss == "plexor"` (Sigil issuer) → existing rotation flow.
- Any other `iss` → re-issue a Plexor bearer via `ITokenIssuer`
  without going back to the IDP. Phase 5+ adds proper OIDC
  refresh-token roundtrip + revocation propagation.
- Malformed JWT (no `iss` parseable) → HTTP 400 with
  `code = "identity.refresh.malformed"`.
- Refresh token unknown or expired → HTTP 401 with
  `code = "identity.refresh.invalid"`.

### Requirement: Permission strings

The system SHALL expose two new permission strings in the
role permission catalog
(`Plexor.Modules.Sigil.Domain.Entities.Role.Permissions`):

- `org.auth.read` — `AuthProviderPermissions.Read`.
- `org.auth.update` — `AuthProviderPermissions.Update`.

The built-in `admin` role carries the `*` wildcard and already
covers both strings (the wildcard is minted by
`Plexor.Migrator/IdentityBootstrapper`); no seeder change
required. Per-role grants (without wildcard) can be added via
the existing role-assignment endpoints.

### Requirement: Standard 401 / 403 / 404 contract

The system SHALL apply the standard authentication /
authorization / tenant-isolation contract on every endpoint in
this capability, matching every other Plexor endpoint:

- **No bearer / invalid bearer** → HTTP 401 with the
  standard `identity.token.*` code from the Sigil capability.
- **Valid bearer, missing permission** → HTTP 403 with
  `code = "identity.permission.denied"`.
- **Valid bearer, valid permission, wrong tenant** → HTTP 404
  (the URL `orgId` doesn't match `currentUser.TenantId`).

The `code` value SHALL be drawn from the identity capability's
stable-code namespace. Clients MUST branch on `code`, never
on `message`.

### Requirement: Migration order

`realm.org_auth_provider_configs` migrations SHALL be applied
after `realm.organizations.id` exists (FK in spirit — the
seeder enumerates orgs) and before any other module's
migrations that depend on the per-tenant auth-provider
config. The `Plexor.Migrator` orders schemas by FK
dependency: `realm` → `sigil` → `atlas` → (future) `ledger`,
`forge`, `outpost`, `shard`. The `InitAuthProviders` migration
is the second Realm migration (the first is
`20260718143631__Initial`).

## Key Entities

### `OrgAuthProviderConfig`

`Plexor.Modules.Realm.Domain.Entities.OrgAuthProviderConfig`
(schema `realm.org_auth_provider_configs`). Fields as listed
above. `UNIQUE (org_id)`.

### `OrgAuthProvider` enum

`Plexor.Modules.Realm.Domain.Entities.OrgAuthProvider`
(PascalCase, bound via the `JsonStringEnumConverter`
registered in `Plexor.Host/Program.cs`). Members:

- `Sigil = 0` — local email + password against
  `sigil.users.password_hash`.
- `Oidc = 1` — external OIDC; Plexor validates the IDP-issued
  JWT against the per-tenant config.

### `AuthProviderId`

`Plexor.Modules.Sigil.Application.AuthProviders.AuthProviderId`
— typed discriminator for `IAuthProvider` selection. Sealed
record with `Value : string`. Two constants:

- `AuthProviderId.Sigil = new("sigil")`
- `AuthProviderId.Oidc = new("oidc")`

### `AuthProviderPermissions`

`Plexor.Shared.Kernel.AuthProviders.AuthProviderPermissions`
mirrors `Plexor.Shared.Kernel.Quotas.QuotaPermissions`. Stable
permission strings: `Read = "org.auth.read"`,
`Update = "org.auth.update"`, `AdminWildcard = "*"`.

### `AuthResolution`

`Plexor.Modules.Sigil.Application.AuthProviders.AuthResolution`
— the resolved-principal record returned by
`IAuthProvider.ResolveAsync`:

- `OrgId : Guid`
- `UserId : Guid`
- `ProviderId : AuthProviderId`
- `IsService : bool` — `false` for OIDC users (always
  human-driven in v0.1); API keys go through the separate
  handler path
- `Roles : IReadOnlyCollection<string>`
- `Permissions : IReadOnlyCollection<string>`
- `TokenLifetime : TimeSpan`

### `OidcFlowContext`

`Plexor.Shared.Kernel.AuthProviders.OidcFlowContext` — the
state-store payload for in-flight OIDC flows:

- `OrgId : Guid`
- `CodeVerifier : string`
- `RedirectUri : string` — the post-callback Plexor URL
- `OriginalRedirect : string` — the operator's deep-link
