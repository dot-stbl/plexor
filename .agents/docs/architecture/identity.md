# Identity — auth, RBAC, tokens, API keys

> Plexor.Modules.Identity owns user accounts, roles, role bindings, SSH keys
> for VM access, and API keys for service-to-service auth (NodeAgent).
> v0.1 ships a local-only auth provider — username/password + JWT bearer +
> refresh tokens. Keycloak / OIDC integration is Phase 2.

This doc is **design only**. Implementation lands in 5 commits (one per
phase) — each reviewable independently.

---

## TL;DR

```
┌──────────────────────────────────────────────────────────────────────┐
│                     Plexor.Host                                     │
│                                                                       │
│  ┌──────────────────┐    ┌─────────────────┐    ┌──────────────────┐  │
│  │  Bearer auth     │    │  Auth API       │    │  Users/Keys API  │  │
│  │  handler         │    │  /login /refresh│    │  /users          │  │
│  │  (JWT or apikey) │ ←→ │  /logout /me    │ ←→ │  /roles          │  │
│  └──────────────────┘    └─────────────────┘    │  /role-bindings  │  │
│           │                      │               │  /ssh-keys      │  │
│           │                      │               │  /api-keys      │  │
│           │                      │               └──────────────────┘  │
│           ▼                      ▼                          │       │
│  ┌─────────────────────────────────────────────────┐   │       │
│  │        IdentityDbContext (sigil schema)          │ ←─┘       │
│  │  users · roles · role_bindings · refresh_tokens   │           │
│  │  api_keys · ssh_keys · signing_keys               │           │
│  └─────────────────────────────────────────────────┘           │
└──────────────────────────────────────────────────────────────────────┘
```

**Two bearer schemes** serve the same `ICurrentUser` shape:
1. **JWT** — `Authorization: Bearer <jwt>`. Short-lived (15 min). For users (UI, CLI).
2. **API key** — `Authorization: Bearer kid_xxxxxxxx.<secret>`. Long-lived (configurable). For service-to-service (NodeAgent ↔ Host).

Both resolved to identical authorization model:
`tenant_id` + `roles[]` + `permissions[]`. `[RequirePermission("compute.vms.create")]` on controller method = 403 when missing.

---

## Scope (v0.1)

| In v0.1 | Out (Phase 2+) |
|---------|-----------------|
| LocalAuthService — bcrypt + JWT + refresh | Keycloak / OIDC / SAML |
| `users` · `roles` · `role_bindings` | External IdP sync |
| `ssh_keys` per user | MFA / WebAuthn |
| `api_keys` per user (replaces NodeAgent JoinToken placeholder) | OAuth scopes / fine-grained resource scopes |
| Tenant-scoped claims | Cross-tenant admin |
| Refresh token reuse detection (family revocation) | Session management UI |
| API key per-tenant permissions | Key expiry notification |
| Schema: `sigil` | Per-tenant signing keys (multi-region) |
| Migrator seeds admin user + `*` role | Self-service signup |
| OpenAPI security schemes (bearer-jwt, bearer-apikey) | — |

---

## Threat model

**Self-hosted Plexor** = single-cluster admin deploys once, controls physical access. Identity protects against:

| Threat | Mitigation |
|--------|------------|
| Password brute-force | bcrypt cost 12 + failed-login lockout (5 → 15 min → 1 h → 24 h) |
| Stolen password | Refresh token rotation + reuse detection (family revocation) |
| Stolen refresh token | Same — refresh tokens are single-use, replay triggers family kill |
| JWT replay | 15 min lifetime + `jti` claim + revocation list for explicit logout |
| API key leak | Single secret shown on creation, SHA-256 hashed in DB, owner can revoke; per-key permissions subset of owner's |
| Cross-tenant access | `tenant_id` claim is authoritative; every query filters by `ICurrentUser.TenantId` |
| Privilege escalation | Permissions are flat strings; `[RequirePermission]` is fail-closed (missing = 403) |
| Insider audit gap | Every login/logout/key event writes `AuditEntry` with `actor_id`, `tenant_id`, source IP |

**Out of scope v0.1**: anomalous login detection, IP-based throttling beyond the per-account lockout, credential rotation policy, password breach DB cross-check.

---

## JWT shape

```jsonc
// Header
{
  "alg": "RS256",
  "kid": "key_2025_q4"          // key id of the signing RSA keypair
}

// Payload
{
  "sub":         "9d4e1a23-...",   // user.id (or api_key.id for service tokens)
  "tid":         "f4b2...",        // tenant.id
  "iss":         "plexor",         // issuer
  "aud":         "plexor-api",     // audience
  "jti":         "0a8b...",        // unique token id (== refresh_tokens.id for refresh tokens)
  "iat":         1735689600,       // issued at (UTC)
  "nbf":         1735689600,       // not before
  "exp":         1735690500,       // expiry (iat + 900s for access)
  "service":     false,            // true if authenticated via API key
  "roles":       ["admin"],        // role names (denormalized snapshot)
  "permissions": ["*"]             // permission strings (flat, no wildcard resolution)
}
```

**Claims are denormalized** at sign time. If a role's `permissions[]` array
changes after a token is issued, that token sees the OLD permission set
until it expires (15 min). Acceptable for v0.1 because permission
changes are infrequent and admin-controlled; refresh tokens rotate
every 7 days so the denormalization window is bounded.

**Signing**: RS256 with a 2048-bit RSA keypair. The keypair is generated
on first start (no keys in DB → seed a `signing_keys` row) and persisted
in `sigil.signing_keys`. Key rotation: every 90 days the signing service
generates a new keypair, marks the old one as `not_after = now`, and
keeps verifying with the previous one until `not_after + access_jwt_lifetime`.

**`kid` header** lets the verifier pick the right public key. The
verifier caches `last 2` public keys to avoid hitting the DB on every
JWT check.

---

## Refresh token rotation + reuse detection

Refresh tokens are opaque 256-bit base64url strings, **not JWTs**. Stored
as `SHA-256(refresh_token)` in `sigil.refresh_tokens.token_hash`.

Rotation flow:

```
login -> issue (access_jwt, refresh_token_A) -> store A (family=f1)
refresh + A -> issue (new_access_jwt, refresh_token_B) -> mark A.revoked_at = now, A.replaced_by = B.id, store B (family=f1)
refresh + A again (replay) -> A.revoked_at != NULL, family f1 still has B active
                              -> REPLAY DETECTED -> revoke ALL family f1 tokens
                              -> 401 + audit event
refresh + B (legitimate) -> issue C, mark B.revoked_at, store C (family=f1)
```

**Refresh tokens are single-use.** Stolen + used = family revocation
forces the attacker and the user to both re-login.

Lifetime: 7 days. After expiry, user re-logs in (new family).

---

## API key (replaces NodeAgent JoinToken)

The current NodeAgent control loop has a placeholder `JoinToken` field
(`NodeAgentWorker.cs`). v0.1 replaces it with API keys:

```csharp
// Generation (API caller)
public sealed record IssueApiKeyResult(ApiKeyRecord Key, string Secret);

// Single token format (Authorization header)
//   "Bearer kid_a8sdf8g2.K9z3NmLp..." 
//   ^          ^       ^
//   scheme     key_id  secret (raw, 43 chars base64url)
//
// Lookup: parse on '.', extract kid_xxx prefix, look up by key_id,
// compare constant-time SHA-256(secret) with stored secret_hash.
```

**Why SHA-256, not bcrypt**: API keys are 32 bytes of cryptographic
random — high-entropy, server-generated. Bcrypt's 2^N cost factor is
wasted on these (it protects against low-entropy human passwords). SHA-256
verifies in ~1 µs vs bcrypt's ~250 ms.

**Permissions** on the key are a **subset** of the owner's permissions.
Issue example:

```
POST /api/v1/users/{user_id}/api-keys
body: {
  "name": "nodeagent-pool-eu-1",
  "permissions": ["nodes.write", "audit.read"],
  "expires_at": "2027-01-01T00:00:00Z"
}
resp: {
  "api_key": { "id": "kid_a8sdf8g2", "name": "...", "permissions": [...] },
  "secret": "K9z3NmLp..."   // SHOWN ONCE — must be saved by caller
}
```

NodeAgent stores the secret at `~/.plexor/credentials` (chmod 0600) and
sends `Authorization: Bearer kid_a8sdf8g2.K9z3NmLp...` on every request.

---

## RBAC permission model

**Format**: `<service>.<resource>.<action>[.<qualifier>]`
**Resolution**: string equality (no wildcards, no inheritance).

| Example | Meaning |
|---------|---------|
| `compute.vms.create` | Create a single VM |
| `compute.vms.create.bulk` | Create ≥ 5 VMs in one request (higher gate) |
| `compute.vms.delete` | Terminate a VM |
| `compute.vms.read` | List / get VM |
| `network.lb.delete` | Delete load balancer |
| `network.sg.write` | Modify security groups |
| `audit.read` | Read audit log |
| `audit.write` | Write audit log (Identity module holds this permission for login events) |
| `*` | Super-admin — short-circuits all checks |

**Permissions are flat strings in `roles.permissions TEXT[]`** (Postgres
text array, no normalization). The role binding just lists them all.

**Why flat**: Audit can record `role R grants permissions A, B, C to
user U on project P` without any hierarchy inference. Changing a role's
permissions is a discrete operation visible in the diff.

**Why no `compute.vms.*` wildcard**: Adding a new `compute.vms.snapshot`
permission would silently grant the wildcard unless we re-audit every
role. Flat permissions make new actions explicit.

**`[RequirePermission]` attribute** (in `Plexor.Shared.Authorization`):

```csharp
[ApiController]
[Route($"{ApiRoutes.Base}/compute/vms")]
public sealed class VmController(ICurrentUser current) : ControllerBase
{
    [HttpPost(Name = "compute-vms-create")]
    [RequirePermission("compute.vms.create")]
    public async Task<ActionResult<VmSummary>> CreateAsync(
        [FromBody] CreateVmRequest request,
        CancellationToken cancellationToken)
    {
        // ... current.UserId, current.TenantId
    }
}
```

Missing permission → 403 Forbidden. Missing authentication → 401 (handled
by bearer middleware before the controller runs).

---

## Database schema (schema `sigil`)

```
users
─────────────────────────────────────────────────────────────────
id              UUID         PK
tenant_id       UUID         FK → sigil.tenants
email           VARCHAR(255) NOT NULL
password_hash   VARCHAR(255) NULL  -- NULL for OAuth-only users (future)
display_name    VARCHAR(128) NOT NULL
status          VARCHAR(16)  NOT NULL  -- 'active' | 'suspended' | 'pending'
failed_login_count INT       NOT NULL DEFAULT 0
locked_until    TIMESTAMPTZ  NULL
last_login_at   TIMESTAMPTZ  NULL
created_at      TIMESTAMPTZ  NOT NULL
updated_at      TIMESTAMPTZ  NOT NULL
UNIQUE (tenant_id, email)
INDEX (tenant_id, status)

roles
─────────────────────────────────────────────────────────────────
id              UUID         PK
tenant_id       UUID         FK → sigil.tenants
name            VARCHAR(64)  NOT NULL
description     TEXT         NULL
permissions     TEXT[]       NOT NULL DEFAULT '{}'
built_in        BOOLEAN      NOT NULL DEFAULT false
created_at      TIMESTAMPTZ  NOT NULL
updated_at      TIMESTAMPTZ  NOT NULL
UNIQUE (tenant_id, name)
INDEX (tenant_id)

role_bindings
─────────────────────────────────────────────────────────────────
id              UUID         PK
tenant_id       UUID         FK → sigil.tenants
user_id         UUID         FK → sigil.users
role_id         UUID         FK → sigil.roles
project_id      UUID         NULL  -- NULL = tenant-wide, otherwise project-scoped
created_at      TIMESTAMPTZ  NOT NULL
UNIQUE (user_id, role_id, project_id)  -- can't bind same role twice on same scope
INDEX (user_id), INDEX (role_id)

refresh_tokens
─────────────────────────────────────────────────────────────────
id              UUID         PK  -- == jti claim when refreshed
user_id         UUID         FK → sigil.users
family_id       UUID         NOT NULL  -- groups rotated tokens
token_hash      CHAR(64)     NOT NULL  -- SHA-256 base64url
expires_at      TIMESTAMPTZ  NOT NULL
revoked_at      TIMESTAMPTZ  NULL
replaced_by     UUID         NULL  -- chains: A -> B -> C
created_at      TIMESTAMPTZ  NOT NULL
INDEX (user_id), INDEX (family_id), INDEX (expires_at)

api_keys
─────────────────────────────────────────────────────────────────
id              UUID         PK  -- == key_id (kid_xxx prefix visible in token)
tenant_id       UUID         FK → sigil.tenants
user_id         UUID         FK → sigil.users  -- owner
name            VARCHAR(128) NOT NULL
secret_hash     CHAR(64)     NOT NULL  -- SHA-256 of the secret
permissions     TEXT[]       NOT NULL
expires_at      TIMESTAMPTZ  NULL
last_used_at    TIMESTAMPTZ  NULL
revoked_at      TIMESTAMPTZ  NULL
created_at      TIMESTAMPTZ  NOT NULL
INDEX (tenant_id, revoked_at), INDEX (user_id)

ssh_keys
─────────────────────────────────────────────────────────────────
id              UUID         PK
user_id         UUID         FK → sigil.users
tenant_id       UUID         NOT NULL  -- denormalized for tenant-scoped queries
name            VARCHAR(128) NOT NULL
fingerprint     CHAR(64)     NOT NULL  -- SHA-256:hash of public key
public_key      TEXT         NOT NULL
last_used_at    TIMESTAMPTZ  NULL
revoked_at      TIMESTAMPTZ  NULL
created_at      TIMESTAMPTZ  NOT NULL
UNIQUE (tenant_id, fingerprint)  -- fingerprint globally unique per RFC 4253

signing_keys
─────────────────────────────────────────────────────────────────
kid             VARCHAR(32)  PK  -- 'key_2025_q4' format
algorithm       VARCHAR(16)  NOT NULL  -- 'RS256'
public_key_pem  TEXT         NOT NULL
private_key_pem TEXT         NULL  -- NULL for non-issuing replicas
created_at      TIMESTAMPTZ  NOT NULL
not_after       TIMESTAMPTZ  NULL  -- NULL = still active
```

**Tenant table** lives in `Plexor.Modules.Tenants`, not Identity.
Identity's foreign keys assume Tenants already exists. **Migration
order** matters: Tenants must migrate before Identity (FK from
`users.tenant_id`).

---

## HTTP API surface

### Auth (anonymous)

| Method | Path | Body | Resp |
|--------|------|------|------|
| `POST` | `/api/v1/auth/login` | `{ email, password, tenant_slug? }` | `{ access_token, refresh_token, expires_in, user, tenant }` |
| `POST` | `/api/v1/auth/refresh` | `{ refresh_token }` | `{ access_token, refresh_token, expires_in, user, tenant }` |
| `POST` | `/api/v1/auth/logout` | `{ refresh_token }` | `204` |

`/login` returns 401 on bad creds, 423 on locked account. `/refresh`
returns 401 on invalid + **revokes family** if reuse detected.

### Current user (authenticated)

| Method | Path | Resp |
|--------|------|------|
| `GET` | `/api/v1/auth/me` | `{ user, tenant, roles, permissions }` |

### Users (authenticated; admin-gated for write)

| Method | Path | Permission |
|--------|------|-----------|
| `POST` | `/api/v1/users` | `identity.users.create` |
| `GET` | `/api/v1/users` | `identity.users.read` |
| `GET` | `/api/v1/users/{id}` | `identity.users.read` (or self) |
| `PATCH` | `/api/v1/users/{id}` | `identity.users.write` (or self for display_name) |
| `POST` | `/api/v1/users/{id}/password` | self only (or admin) |

### SSH keys (authenticated; self-service + admin)

| Method | Path | Permission |
|--------|------|-----------|
| `POST` | `/api/v1/users/{id}/ssh-keys` | self or `identity.users.write` |
| `GET` | `/api/v1/users/{id}/ssh-keys` | self or `identity.users.read` |
| `DELETE` | `/api/v1/users/{id}/ssh-keys/{keyId}` | self or `identity.users.write` |

### API keys (authenticated; self-service + admin)

| Method | Path | Permission |
|--------|------|-----------|
| `POST` | `/api/v1/users/{id}/api-keys` | self or `identity.users.write` |
| `GET` | `/api/v1/users/{id}/api-keys` | self or `identity.users.read` (returns metadata only) |
| `DELETE` | `/api/v1/users/{id}/api-keys/{keyId}` | self or `identity.users.write` |

### Roles + bindings (admin-gated)

| Method | Path | Permission |
|--------|------|-----------|
| `POST` | `/api/v1/roles` | `identity.roles.create` |
| `GET` | `/api/v1/roles` | `identity.roles.read` |
| `PATCH` | `/api/v1/roles/{id}` | `identity.roles.write` (built_in: immutable) |
| `DELETE` | `/api/v1/roles/{id}` | `identity.roles.write` (built_in: refuse) |
| `POST` | `/api/v1/role-bindings` | `identity.roles.assign` |
| `DELETE` | `/api/v1/role-bindings/{id}` | `identity.roles.assign` |

### OpenAPI security scheme

Every authenticated endpoint declares:

```yaml
security:
  - bearer_jwt: []
  - bearer_apikey: []
```

Anonymous endpoints (`/auth/login`, `/auth/refresh`) declare:
```yaml
security: []
```

Two distinct security scheme components in `openapi.yaml`:

```yaml
components:
  securitySchemes:
    bearer_jwt:
      type: http
      scheme: bearer
      bearerFormat: JWT
    bearer_apikey:
      type: http
      scheme: bearer
      bearerFormat: kid_xxx.secret
```

---

## `ICurrentUser` — common shape for both schemes

```csharp
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid TenantId { get; }
    Guid? ProjectId { get; }  // null unless request was scoped (rare)
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool IsService { get; }  // true if authenticated via API key
}
```

Same shape regardless of which bearer scheme authenticated the request.
Both `Authorization: Bearer <jwt>` and `Authorization: Bearer kid_xxx.secret`
flow through a single ASP.NET Core `AuthenticationHandler` that produces
this `ClaimsPrincipal`.

---

## Identity ↔ Audit integration

Identity module writes audit events for:

| Event | action | outcome |
|-------|--------|---------|
| Login success | `auth.login.succeeded` | succeeded |
| Login failure | `auth.login.failed` | failed |
| Account locked | `auth.account.locked` | failed |
| Refresh rotation | `auth.refresh.rotated` | succeeded |
| Refresh reuse detected | `auth.refresh.replayed` | failed |
| Logout | `auth.logout` | succeeded |
| Password changed | `auth.password.changed` | succeeded |
| API key issued | `auth.apikey.issued` | succeeded |
| API key revoked | `auth.apikey.revoked` | succeeded |
| SSH key added | `auth.sshkey.added` | succeeded |
| SSH key revoked | `auth.sshkey.revoked` | succeeded |

These entries satisfy SOC2 / ISO 27001 audit trail requirements and
are queryable through `GET /api/v1/audit` (planned in Plexor.Modules.Audit
Phase 1 controller; deferred until Audit controller lands).

---

## Identity ↔ Tenants

**v0.1**: 1:1 user → tenant. No cross-tenant users. No user
invitations (admin provisions users directly). Email + tenant_slug
uniquely identifies a user — same email can exist in different tenants
without collision.

`tenant_slug` in `/auth/login` resolves to `tenant_id` before password
verification. This avoids email collisions and matches YC pattern.

`Plexor.Modules.Tenants` owns the tenant table; Identity references
`sigil.tenants.id` as FK. Migration order: **Tenants first, then
Identity**. Audited via `dotnet ef migrations list` on `sigil` schema.

---

## Authorization checks (2 layers)

**Layer 1 — Authentication** (middleware): `IsAuthenticated` true,
`HttpContext.User` populated with `ICurrentUser` claims. Failed →
401 Unauthorized.

**Layer 2 — Authorization** (per-endpoint): `[RequirePermission("...")]`
attribute on controller method, evaluated by ASP.NET Core authorization
policy. Failed → 403 Forbidden. Missing → 403 (fail-closed).

`[RequirePermission]` resolves to a single permission string from the
token's `permissions[]` claim. Admin users (with `*` permission) bypass
the check.

`Plexor.Shared.Authorization` library owns the attribute + policy
registration. Wired in `Plexor.Shared.Composition.AddPlexorAuthorization()`.

---

## Password policy

- **Bcrypt cost factor 12** (~250 ms hash, ~125 ms verify on modern CPU)
- **Minimum 12 chars**, no complexity rules (per NIST SP 800-63B)
- No password history check in v0.1 (planned v0.2)
- No breach DB cross-check in v0.1 (planned v0.2)
- **Failed-login lockout**: 5 consecutive failures → 15 min lockout,
  10 failures → 1 h, 15 failures → 24 h. Counter resets on success.

Password reset flow: v0.1 admin-only (`POST /api/v1/users/{id}/password`).
Self-service reset via email link is v0.2 (requires SMTP config).

---

## NodeAgent migration: JoinToken → API key

Current state (`Plexor.Shared.NodeApi/NodeContracts.cs`):
```csharp
public sealed record JoinRequest(string Token);  // placeholder
```

**Migration plan** (separate commit when Identity lands):
1. `JoinRequest.Token` becomes `JoinRequest.ApiKey` (string, accepts
   `kid_xxx.secret` format).
2. NodeAgent CLI config changes from `--JoinToken=<token>` to
   `--ApiKey=~/.plexor/credentials` (file path).
3. Host `/api/v1/nodes/join` endpoint switches from in-memory token
   check to API key lookup. The existing in-memory node registry
   (`InMemoryNodeRegistry`) stays; only the auth changes.

---

## Implementation phases (5 commits)

1. **Domain layer** — entities (User, Role, RoleBinding, SshKey, ApiKey,
   RefreshToken, SigningKey), value objects (PasswordHash, Permission
   list, TenantSlug), domain events. Pure C#, no infrastructure.

2. **Persistence** — `IdentityDbContext`, `IEntityTypeConfiguration<T>`
   for each entity, `Identity_InitialSchema` migration. snake_case,
   schema `sigil`, full index coverage.

3. **Auth infrastructure** — `IPasswordHasher` (bcrypt wrapper),
   `IJwtSigningService` (RS256 + rotating keypair), `IRefreshTokenStore`
   (DB-backed), `IAuthenticationService` (login / refresh / logout),
   `ICurrentUser` + `BearerAuthenticationHandler`, ASP.NET Core auth wiring.

4. **Application endpoints + Controllers** — request/response records,
   FluentValidation, controllers for Auth / Users / Roles / RoleBindings
   / SshKeys / ApiKeys, DI installers.

5. **Migrator seed** — `IdentityAdminSeeder` registered in `Plexor.Host.Migrator`
   seeds the first admin user + `*` role on first deploy. Idempotent.

Each commit is reviewable independently. Build must be clean
between commits (`dotnet build plexor.slnx - 0 warnings, 0 errors`).

---

## Open questions / future work

1. **Cross-tenant users** (v0.2): A user belongs to multiple tenants with
   different role sets per tenant. Adds a `tenant_memberships` table;
   JWT's `tid` claim becomes the **currently selected** tenant at
   request time (UX: tenant switcher in UI).

2. **Self-service password reset** (v0.2): SMTP-driven email link with
   single-use token, 30-min lifetime. Requires `SmtpOptions` config.

3. **Keycloak / OIDC bridge** (v0.2+): External IdP for organizations
   with existing identity. Identity module grows a second provider
   behind the same `IAuthenticationService` interface.

4. **MFA / WebAuthn** (v0.2+): TOTP enrollment, recovery codes,
   hardware keys. Layered on top of `password_hash` (TOTP secret stored
   alongside).

5. **OAuth scopes** (v0.3+): For 3rd-party API clients (different from
   service-to-service API keys). Adds `oauth_clients` + `oauth_tokens`.

---

## Related

- `.agents/docs/modules.md` — Identity module spec (entities, endpoints, permissions)
- `.agents/docs/architecture/persistence.md` — schema-per-module, Specification pattern
- `.agents/docs/architecture/specification-integration.md` — Spec+Filter controller pattern
- `.agents/docs/architecture/traffic.md` — HTTP/JSON REST, no auth at v0.x inter-host
- `src/modules/Plexor.Modules.Identity/` — implementation
- `src/modules/Plexor.Modules.Tenants/` — tenant module (must migrate first)
- `src/modules/Plexor.Modules.Audit/` — audit module (writes depend on identity)
- `src/shared/Plexor.Shared.Authorization/` — `[RequirePermission]` attribute