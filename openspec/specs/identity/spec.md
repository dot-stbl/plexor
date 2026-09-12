# Capability: identity

## Purpose

Authentication, authorization, and credential storage for Plexor.
Plexor ships a local identity provider (`sigil`) for users (email +
password, JWT bearer + refresh token rotation) and for service
accounts (long-lived API keys used by `Plexor.NodeAgent` ↔
`Plexor.Host`). Both schemes resolve to one shape: `ICurrentUser`
with user id, org id, roles, permissions, and an `IsService` flag.

A second, optional OIDC auth-provider backend for external
identity providers (Keycloak, Authentik, etc.) is **not** part of
this capability — it is `openspec/specs/auth-providers/spec.md`
(Phase 4.6+).

This capability is owned by `Plexor.Modules.Sigil`. Schema name in
SQL and migrations: `sigil`. C# concept names: `User`, `Role`,
`RoleBinding`, `ApiKey`, `SshKey`, `RefreshToken`, `SigningKey`.

## Requirements

### Requirement: Local email + password authentication

The system SHALL authenticate users with an email address and a
password verified against the `sigil.users.password_hash` column
(`Plexor.Modules.Sigil.Domain.Entities.User.PasswordHash`).

Passwords MUST be hashed with bcrypt at cost factor 12 (~250 ms on
modern CPU). New passwords MUST be at least 12 characters; the
system SHALL NOT impose composition rules (no required digits,
symbols, or case mixes), per NIST SP 800-63B.

The Migrator SHALL seed an initial admin user + `*` super-admin
role on first deploy so a single-host install can authenticate
without external IdP configuration.

### Requirement: JWT access tokens (ES256, 15 minutes)

The system SHALL issue ES256 (ECDSA over P-256) signed JWT access
tokens at login time with a 15-minute lifetime.

The JWT header SHALL carry `kid` (key id, format `key_YYYY_Qn`).
The JWT payload SHALL include the following claims:

- `sub` — user id (UUID v7) OR API-key id for service tokens.
- `tid` — organization id.
- `iss` — `"plexor"`.
- `aud` — `"plexor-api"`.
- `jti` — unique token id; equals the `RefreshToken.Id` for
  refresh operations.
- `iat`, `nbf`, `exp` — issued-at, not-before, expiry.
- `service` — `true` if the bearer is an API key, `false` for
  user JWTs.
- `roles[]` — role names from `Role.Name` (denormalized snapshot
  at sign time).
- `permissions[]` — flat permission strings from
  `Role.Permissions` (denormalized snapshot at sign time).

The signing key SHALL be an ECDSA P-256 keypair generated on
first start and persisted in `sigil.signing_keys` (`SigningKey`
entity). The verifier caches the most recent two public keys
in-memory and falls back to the DB only when the JWT's `kid` is
unknown.

### Requirement: Refresh tokens with single-use rotation and family revocation

The system SHALL issue opaque refresh tokens alongside JWT access
tokens. Refresh tokens MUST be 256-bit base64url strings stored as
SHA-256 hash in `sigil.refresh_tokens.token_hash`
(`Plexor.Modules.Sigil.Domain.Entities.RefreshToken.TokenHash`).

Refresh tokens MUST be single-use. On refresh:

1. The presented token's hash is looked up. If not found → 401.
2. If found and `revoked_at IS NULL`, a new refresh token is
   issued; the old token's `revoked_at` is set to `now`, and
   `replaced_by` points to the new token id. Both tokens share
   the same `family_id`.
3. If found but `revoked_at IS NOT NULL` **and** another token
   in the same `family_id` is active, the request is a replay
   attack — the system SHALL revoke every token in the family
   (set `revoked_at = now`) and return 401.

Refresh token lifetime SHALL be 7 days. After expiry the user MUST
re-login (a new family).

### Requirement: API keys for service-to-service auth

The system SHALL support long-lived API keys for service-to-service
authentication (NodeAgent ↔ Host). API keys SHALL be issued via
`POST /api/v1/users/{user_id}/api-keys`.

The bearer format SHALL be `kid_<key_id>.<secret>` where
`<key_id>` is the API key id (UUID v7, prefixed `kid_`) and
`<secret>` is a 32-byte random base64url string (43 chars). The
`Authorization` header is `Bearer kid_<key_id>.<secret>`.

The system SHALL persist only the SHA-256 hash of the secret
(`Plexor.Modules.Sigil.Domain.Entities.ApiKey.SecretHash`), never
the plaintext. Constant-time comparison SHALL happen in the
authentication handler.

API key permissions SHALL be a subset of the owner's effective
permissions at issue time. Issuing a key with permissions the
owner does not hold SHALL return 403.

The raw secret SHALL be shown in the `POST` response exactly
once; the server SHALL NOT be able to retrieve it later.

### Requirement: Failed-login lockout

The system SHALL increment `users.failed_login_count` on each
consecutive failed login (a successful login resets the counter).
Lockout thresholds SHALL be:

| Failed count | Lockout duration |
|-------------|------------------|
| 5 | 15 minutes |
| 10 | 1 hour |
| 15 | 24 hours |

A locked account SHALL return HTTP 423 with `code =
"identity.credentials.locked"`. The lockout window SHALL be stored
in `users.locked_until` (UTC). Successful login during lockout
SHALL NOT reset the counter until the lockout expires.

### Requirement: Flat permission strings with no wildcards

The system SHALL model RBAC permissions as flat strings of the
form `<service>.<resource>.<action>[.<qualifier>]`. Examples:

- `compute.vms.create`
- `compute.vms.create.bulk`
- `compute.vms.delete`
- `compute.vms.read`
- `network.lb.delete`
- `network.sg.write`
- `audit.read`
- `audit.write`
- `quotas.read`
- `quotas.assign.org`

The system SHALL NOT resolve wildcards. `compute.vms.*` does
**not** grant `compute.vms.snapshot` — adding a new action is an
explicit permission grant. The only wildcard is `*` for the
super-admin role; presence of `*` in the `permissions[]` claim
SHALL short-circuit every `[RequirePermission]` check.

The `permissions[]` claim is denormalized at JWT sign time.
Permission changes in a `Role` row SHALL NOT affect an
already-issued token until it expires (15 min window) or is
refreshed.

### Requirement: RoleBinding with nullable TeamId / FolderId scope

The system SHALL attach a user to a role via a `RoleBinding`
(`Plexor.Modules.Sigil.Domain.Entities.RoleBinding`) row carrying
nullable `TeamId` and `FolderId` foreign keys.

| `TeamId` | `FolderId` | Scope |
|----------|------------|-------|
| `null` | `null` | Org-wide (applies to operations across the org) |
| set | `null` | Team-wide (applies to operations scoped to that team) |
| set | set | Folder-scoped (applies to operations scoped to that folder) |

The DB SHALL enforce `UNIQUE (user_id, role_id, team_id,
folder_id)` so the same role cannot be bound to the same scope
twice. A user's effective permissions are the union of all
bindings' roles' permissions.

### Requirement: `[RequirePermission]` attribute is the authorization gate

The system SHALL gate controller methods on a permission claim
using `Plexor.Shared.Authorization.RequirePermissionAttribute`,
evaluated by `PermissionPolicyProvider` and
`PermissionAuthorizationHandler` in `Plexor.Shared.Authorization`.

Missing authentication SHALL return 401. Missing permission SHALL
return 403 (fail-closed — absence of permission is not implicit
grant). An attribute with multiple permission names SHALL combine
with AND semantics: missing any one permission produces 403.

The `RequirePermissionAttribute` is `sealed`, primary-constructed,
takes `params string[] permissions`, and exposes a
non-empty-trimmed `IReadOnlyList<string> Permissions`.

### Requirement: ICurrentUser is identical for JWT and API-key auth

The system SHALL resolve both JWT and API-key bearer schemes to a
single `ICurrentUser` shape (`Plexor.Modules.Sigil.Application.Abstractions.ICurrentUser`)
exposed per-request:

```csharp
Guid UserId { get; }
Guid OrgId { get; }
Guid? TeamId { get; }
Guid? FolderId { get; }
IReadOnlyCollection<string> Roles { get; }
IReadOnlyCollection<string> Permissions { get; }
bool IsService { get; }   // true iff authenticated via API key
```

The shape MUST be identical for both schemes; downstream code
SHALL NOT branch on which scheme authenticated the request — only
`IsService` distinguishes a human user from a service account.

### Requirement: Permission claim wire name `permission`

The system SHALL use the wire-format claim type `permission`
(`Plexor.Shared.Authorization.AuthorizationClaimNames.PermissionClaim`)
to carry each permission string in the JWT. The JWT signer and
the authorization handler SHALL both read this same name; the
value MUST be identical in both assemblies on purpose (the shared
authorization module has no dependency on the Identity module,
and vice versa).

A pre-commit unit test SHALL detect any drift between
`AuthorizationClaimNames.PermissionClaim` and the Identity
module's own claim-name constant.

### Requirement: Stable error codes

The system SHALL emit ProblemDetails with stable, dot.case
machine-readable `code` values that clients can branch on. Codes
SHALL be drawn from a single namespace:

- `identity.credentials.invalid` — wrong email or password.
- `identity.credentials.locked` — account is locked.
- `identity.credentials.provider_mismatch` — wrong auth flow for
  the tenant's configured provider.
- `identity.token.expired` — JWT `exp` in the past.
- `identity.token.replay_detected` — refresh-token reuse; family
  revoked.
- `identity.permission.denied` — `[RequirePermission]` 403.

The `code` SHALL NOT be derived from human messages. Clients MUST
branch on `code`, never on `message` (ProblemDetails `detail`).

### Requirement: API key replaces JoinToken placeholder

The NodeAgent ↔ Host control loop SHALL authenticate with an API
key (`Authorization: Bearer kid_<key_id>.<secret>`), NOT a
`JoinToken` placeholder. The migration from `JoinToken` to API
key is part of this capability: the existing
`Plexor.Shared.NodeApi.NodeContracts.JoinRequest.Token` field is
replaced by `JoinRequest.ApiKey`, and the host
`POST /api/v1/compute/clusters/join` endpoint validates the
bearer against `sigil.api_keys` instead of the in-memory token
registry.

## Key Entities

### `User`

`Plexor.Modules.Sigil.Domain.Entities.User`
(schema `sigil.users`). Fields:

- `Id : Guid` — UUID v7, PK.
- `OrgId : Guid` — FK to `realm.organizations.id`. 1:1 in v0.1.
- `Email : Email` — validated, lowercased.
- `DisplayName : string` — UI label.
- `Status : string` — `"active"`, `"suspended"`, `"pending"`.
- `PasswordHash : PasswordHash?` — bcrypt, null for OAuth-only.
- `FailedLoginCount : int` — consecutive failures.
- `LockedUntil : DateTimeOffset?` — lockout window end.
- `LastLoginAt : DateTimeOffset?` — last successful login.
- `PasswordChangedAt : DateTimeOffset?` — drives forced rotation.
- `CreatedAt : DateTimeOffset`, `UpdatedAt : DateTimeOffset`.

`UNIQUE (OrgId, Email)`.

### `Role`

`Plexor.Modules.Sigil.Domain.Entities.Role`
(schema `sigil.roles`). Fields:

- `Id : Guid` — UUID v7, PK.
- `OrgId : Guid` — FK to `realm.organizations.id`.
- `Name : string` — unique per org (`admin`, `viewer`,
  `compute.editor`).
- `Description : string?` — optional.
- `Permissions : IReadOnlyList<PermissionScope>` — flat
  permission strings; `*` for super-admin.
- `BuiltIn : bool` — `true` for Migrator-seeded roles; immutable,
  cannot be deleted (DELETE returns 409).
- `CreatedAt : DateTimeOffset`, `UpdatedAt : DateTimeOffset`.

`UNIQUE (OrgId, Name)`. Permissions stored as Postgres `TEXT[]`.

### `RoleBinding`

`Plexor.Modules.Sigil.Domain.Entities.RoleBinding`
(schema `sigil.role_bindings`). Fields:

- `Id : Guid` — UUID v7, PK.
- `OrgId : Guid` — denormalized for org-scoped queries.
- `UserId : Guid` — FK to `sigil.users.id`.
- `RoleId : Guid` — FK to `sigil.roles.id`.
- `TeamId : Guid?` — optional team scope.
- `FolderId : Guid?` — optional folder scope.
- `CreatedAt : DateTimeOffset`.

`UNIQUE (UserId, RoleId, TeamId, FolderId)`. The user's effective
permissions are the union of every bound role's permissions.

### `RefreshToken`

`Plexor.Modules.Sigil.Domain.Entities.RefreshToken`
(schema `sigil.refresh_tokens`). Fields:

- `Id : Guid` — UUID v7, PK; equals `jti` claim.
- `UserId : Guid` — FK to `sigil.users.id`.
- `FamilyId : Guid` — shared by all rotations of one login.
- `TokenHash : string` — SHA-256 base64url, never plaintext.
- `ExpiresAt : DateTimeOffset` — default 7 days.
- `RevokedAt : DateTimeOffset?` — `null` while active.
- `ReplacedBy : Guid?` — next token in the rotation chain.
- `CreatedAt : DateTimeOffset`.

### `ApiKey`

`Plexor.Modules.Sigil.Domain.Entities.ApiKey`
(schema `sigil.api_keys`). Fields:

- `Id : Guid` — UUID v7, PK; becomes `kid_<key_id>` prefix.
- `OrgId : Guid` — tenant scope.
- `UserId : Guid` — owner.
- `Name : string` — human label.
- `SecretHash : string` — SHA-256 of raw secret, never plaintext.
- `Permissions : IReadOnlyList<PermissionScope>` — subset of
  owner's effective permissions.
- `ExpiresAt : DateTimeOffset?` — null = no expiry.
- `LastUsedAt : DateTimeOffset?` — debounced to 60s.
- `RevokedAt : DateTimeOffset?` — null while active.
- `CreatedAt : DateTimeOffset`.

### `SshKey`

`Plexor.Modules.Sigil.Domain.Entities.SshKey`
(schema `sigil.ssh_keys`). Fields:

- `Id : Guid` — UUID v7, PK.
- `UserId : Guid` — owner.
- `OrgId : Guid` — denormalized; UNIQUE constraint co-located.
- `Name : string` — label (`workstation`, `github-actions`).
- `Fingerprint : string` — SHA-256 of public key, base64url.
- `PublicKey : string` — full OpenSSH-format string.
- `LastUsedAt : DateTimeOffset?`.
- `RevokedAt : DateTimeOffset?`.
- `CreatedAt : DateTimeOffset`.

`UNIQUE (OrgId, Fingerprint)`.

### `SigningKey`

`Plexor.Modules.Sigil.Domain.Entities.SigningKey`
(schema `sigil.signing_keys`). Fields:

- `Kid : string` — PK, format `key_YYYY_Qn`.
- `Algorithm : string` — `"ES256"` in v0.1.
- `PublicKeyPem : string` — PKCS#8 SubjectPublicKeyInfo.
- `PrivateKeyPem : string?` — PKCS#8 PEM; null on rotated keys.
- `CreatedAt : DateTimeOffset`.
- `NotAfter : DateTimeOffset?` — null = still active signer.