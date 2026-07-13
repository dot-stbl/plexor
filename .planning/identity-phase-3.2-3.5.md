# Identity Phase 3.2-3.5 — Contracts + Signing

## Scope

Build the auth contracts and JWT signing infrastructure that Phase 3.6
(bearer handler) and Phase 4 (endpoints) will plug into. v0.1 stores
signing keys in the `SigningKey` entity (table already migrated).

### What's in scope

- **Contracts** (Application layer):
  `IAuthenticationService` (login + refresh + revoke), `IRefreshTokenStore`
  (rotation + replay detection), `IPasswordHasher` (wraps
  `PasswordHasher<User>` from `Microsoft.Extensions.Identity.Core`).
- **Signing** (Application + Infrastructure):
  `IJwtSigningService` + ECDSA-based `JwtSigningService` using
  `System.IdentityModel.Tokens.Jwt`. ECDSA P-256 keys (smaller than
  RSA-2048, faster verification, modern default).
- **Storage** (Infrastructure):
  `IRefreshTokenStore` EF-backed implementation. `SigningKeyRepository`
  for rotation + lookup.
- **Key bootstrap** (Infrastructure):
  On startup, ensure at least one active signing key exists. Generate
  ECDSA keypair via `ECDsa.Create(ECCurve.NamedCurves.nistP256)`, export
  PKCS#8 PEM, store. The private key is **already encrypted** via
  `IDataProtector` (we already have `Microsoft.AspNetCore.DataProtection`
  registered in `Plexor.Host/Program.cs` for `SigningKey.PrivateKeyPem`).
- **DI** in `Plexor.Host/Program.cs`:
  `AddSigilApplicationCore` + `AddSigilInfrastructureCore` installers
  (currently they don't exist as a single entry — wire them up).

### What's NOT in scope

- BearerAuthenticationHandler (Phase 3.6) — needs signing service ready first
- `[RequirePermission]` attribute (Phase 3.7) — Authorization policy layer
- `/auth/login`, `/auth/refresh` controllers (Phase 4)
- API key → bearer translation (deferred — NodeAgent will continue to
  use `kid_xxx.<secret>` style until Phase 4 ties it together)

## Built-in vs custom — what we take, what we write

Following `prefer-built-ins-over-hand-rolled.md`: take a built-in when
it solves the problem cleanly; write our own when the built-in has a
different shape or pulls in too much.

### TAKING (built-in)

| Component | Where | Why |
|-----------|-------|-----|
| `PasswordHasher<User>` | `Microsoft.Extensions.Identity.Core 10.0.9` | PBKDF2 + HMAC-SHA256, 100k iterations (default), 16-byte salt, 32-byte subkey. Already wrapped in `IPasswordHasher` interface in this phase. |
| `JwtSecurityTokenHandler` / `SecurityTokenDescriptor` / `SecurityToken` | `System.IdentityModel.Tokens.Jwt 8.6.1` | De-facto MS-stack standard. ES256 fully supported. Wraps signing/verify — we just provide the `ECDsaSecurityKey`. |

### WRITING (custom)

| Component | Why custom |
|-----------|-----------|
| `IUserStore<User>` / `UserManager<User>` | Built-in wants `IdentityUser<Guid>` with pre-defined `IdentityUserClaim`, `IdentityUserRole`, `IdentityUserLogin`, `IdentityUserToken` tables. We have a custom `User` entity (Guid Id, snake_case columns, separate `role_bindings` table). Mapping cost > saving. |
| `SignInManager<User>` | Built-in orchestrates cookies + claims identity. We have a pure JWT flow with no cookies — different orchestration. |
| `RoleManager<Role>` | Same reason as `UserManager`. |
| `IdentityDbContext<User>` | Tables are strictly ours: snake_case, custom FKs. |
| `AddJwtBearer` middleware | We want `BearerAuthenticationHandler` that handles BOTH `<jwt>` and `kid_xxx.<secret>` API-key format in Phase 3.6. Built-in `JwtBearerHandler` only validates JWTs. |
| Lockout (`User.LockoutEnd`) | We have our own `failed_login_count` + `locked_until` columns on `User`. Not using built-in lockout. |
| `IdentityErrorDescriber` | We have our own `IdentityException` with string codes (`"identity.email.invalid"`). |
| Token providers (`IUserTwoFactorStore`, etc.) | v0.1 doesn't ship email/SMS flows. |

### Net result

Two built-in components wrapped (`PasswordHasher`, JWT handler). No
`AddIdentity<TUser>()`, no `SignInManager`, no built-in DbContext, no
identity tables. Our schema stays ours.

## Architecture decisions

### 1. ECDSA P-256 over RSA-2048

**Why**: 256-bit ECDSA = ~112-bit security (NIST), 256-byte signatures,
256-byte public keys (vs RSA-2048's 256-byte signature, 256-byte key,
but slower verify by ~10x). Modern JWT libs default to ECDSA. JWS
`"alg": "ES256"` is universally supported by libraries we'll integrate
with (NextAuth, etc.).

### 2. JWT shape

```json
{
  "alg": "ES256",
  "typ": "JWT",
  "kid": "key_2026_q3"
}
{
  "iss": "plexor",
  "sub": "<user-guid>",
  "tid": "<org-guid>",
  "role": ["admin", "operator"],
  "permission": ["vm.create", "vm.delete"],
  "iat": 1752412800,
  "exp": 1752413700,
  "service": "false"
}
```

Lifetime: **15 minutes** for access tokens (sliding window — refresh
extends). Refresh tokens: **30 days** (rotated on every use, replay
detection triggers account lock).

### 3. `IPasswordHasher` wraps `PasswordHasher<User>`

Don't reinvent PBKDF2. The MS Identity Core library ships a
`PasswordHasher<TUser>` that uses PBKDF2 with HMAC-SHA256, 100k
iterations (default), 16-byte salt, 32-byte subkey. We wrap it in
`IPasswordHasher` so Application code doesn't directly depend on
`Microsoft.Extensions.Identity.Core`.

### 4. Refresh token = random 32 bytes, base64url

256 bits of entropy = 43-char base64url string. Stored as
`RefreshToken.TokenHash` (SHA-256 of raw token). Only the hash hits
the DB — same pattern as the rest of the field (defense in depth).

### 5. Signing key bootstrap — "first writer wins" in v0.1

On startup, `ISigningKeyBootstrapper` checks `SELECT COUNT(*) FROM
signing_keys WHERE not_after IS NULL`. If zero, generate a new
ECDSA keypair and insert it. If multiple hosts start simultaneously,
they both try to insert — only the first wins because of the unique
`kid` constraint (`kid = "key_YYYY_Qn"`). Subsequent hosts see
the existing key and reuse it.

For Phase 4+ we add a distributed-lock (advisory lock in Postgres or
Redis); not needed for single-host v0.1.

## Files to create

### Contracts (Application layer — `Plexor.Modules.Sigil.Application/`)

```
Abstractions/
├── IAuthenticationService.cs           # LoginAsync, RefreshAsync, LogoutAsync
├── IRefreshTokenStore.cs                # IssueAsync, RotateAsync, RevokeAsync, FindByHashAsync
├── IPasswordHasher.cs                   # HashPassword, VerifyHashedPassword
├── IJwtSigningService.cs                # IssueAsync (returns compact JWT), GetActiveSigningKeyAsync
├── ISigningKeyRepository.cs             # GetActiveAsync, ListActiveAsync, AddAsync
└── Models/
    ├── LoginRequest.cs                  # Email, Password, TenantId
    ├── LoginResult.cs                   # AccessToken, RefreshToken, ExpiresAt
    ├── RefreshRequest.cs                # RefreshToken
    ├── RefreshResult.cs                 # AccessToken, RefreshToken, ExpiresAt
    └── IssuedTokens.cs                  # internal record (AccessToken + RefreshToken + ExpiresAtUtc)
```

### Services (Infrastructure layer — `Plexor.Modules.Sigil.Infrastructure/`)

```
Auth/
├── PlexorPasswordHasher.cs              # wraps PasswordHasher<User>
├── EfRefreshTokenStore.cs               # implements IRefreshTokenStore
├── JwtSigningService.cs                 # ECDSA + ES256 + kid header
├── EfSigningKeyRepository.cs            # implements ISigningKeyRepository
├── SigningKeyBootstrapper.cs            # IHostedService — ensures 1 active key
├── RefreshTokenHasher.cs                # static — SHA-256(rawToken) -> base64url
└── TokenGenerator.cs                    # static — 32 bytes -> base64url string
```

### DI installers

```
Plexor.Modules.Sigil.Application/Installers/
└── SigilApplicationInstaller.cs         # AddSigilApplicationCore(IServiceCollection, IConfiguration)

Plexor.Modules.Sigil.Infrastructure/Installers/
└── SigilInfrastructureInstaller.cs      # AddSigilInfrastructureCore(IServiceCollection, IConfiguration)
```

### Tests

```
tests/unit/Plexor.Modules.Sigil.Unit/
├── Auth/
│   ├── PlexorPasswordHasherTests.cs     # Hash+Verify roundtrip, wrong password rejects
│   ├── JwtSigningServiceTests.cs        # Issue -> verify with same key, kid in header
│   ├── RefreshTokenHasherTests.cs       # Deterministic, base64url-safe
│   └── TokenGeneratorTests.cs           # 32 bytes -> 43 chars, URL-safe
└── Auth/EfRefreshTokenStoreTests.cs    # Rotation logic + replay detection (uses TestDbContext or in-memory)
```

## Acceptance criteria

1. **`dotnet build plexor.slnx`** — 0 warnings, 0 errors
2. **All 5 unit test files pass** — `dotnet test tests/unit/Plexor.Modules.Sigil.Unit`
3. **Manual smoke (Postman or curl)** — actually NOT possible yet because
   the bearer handler hasn't shipped (Phase 3.6). Smoke is deferred.
4. **Migrations unchanged** — Phase 3.2-3.5 doesn't change the schema.
   `signing_keys` and `refresh_tokens` tables are already migrated
   (Phase 2b).
5. **Boot test** — `dotnet run --project src/host/Plexor.Host` logs
   "signing key kid=key_2026_q3 is active" on first start.
6. **No mock data** — everything real. JWT signed with real ECDSA key,
   verified with real public key.

## Phase ordering within 3.2-3.5

```
3.2.a  Installers + IServiceCollection wiring                     ~30 min
3.2.b  IPasswordHasher + PlexorPasswordHasher + tests              ~30 min
3.2.c  IRefreshTokenStore + EfRefreshTokenStore + tests            ~60 min
       (rotation logic + replay detection is the trickiest part)
3.2.d  RefreshTokenHasher + TokenGenerator (static utilities)      ~15 min
3.3.a  IJwtSigningService + JwtSigningService + tests              ~60 min
       (ECDSA key loading from PEM, JWT issuance, kid header)
3.3.b  ISigningKeyRepository + EfSigningKeyRepository              ~30 min
3.4.a  SigningKeyBootstrapper (HostedService)                      ~30 min
3.4.b  Wire bootstrapper + signing service in Program.cs           ~15 min
3.5    Boot test + integration check                               ~15 min
```

Total: **~4.5 hours**, 5 commits.

## Pre-existing tech debt to NOT address here

- **CRITICAL/MAJOR warnings elsewhere** — out of scope, separate PR
- **Tests for HttpContextCurrentUser** — it's trivial, skip
- **Audit logging on auth events** — Phase 4 (controller) or Phase 5 (atlas module)
- **Rate limiting on /auth/login** — Phase 4 (when endpoint lands)

## Migration to Phase 3.6+

After this phase, the **signing service + auth service + refresh store
are ready**. Phase 3.6 adds:
- `BearerAuthenticationHandler : AuthenticationHandler<BearerOptions>`
- Reads `Authorization: Bearer <jwt-or-kid-prefixed-secret>`
- For JWT: `JwtSigningService.VerifyAsync(token) → ClaimsPrincipal`
- For `kid_xxx.<secret>`: lookup in ApiKey store, build principal with
  `IsService=true`, no Roles, only Permissions from the key
- `services.AddAuthentication("Bearer").AddBearer(...)` in Program.cs

Phase 4 (endpoints) then adds:
- `POST /auth/login` → `IAuthenticationService.LoginAsync(LoginRequest)`
- `POST /auth/refresh` → `IAuthenticationService.RefreshAsync(RefreshRequest)`
- `POST /auth/logout` → `IAuthenticationService.LogoutAsync(refreshToken)`
- `GET /auth/me` → reads `ICurrentUser`, returns summary

## Open questions for review

1. **ECDSA vs RSA** — OK? Or want RSA-2048 for compatibility with
   legacy JWT libraries? (ECDSA is the modern default; NodeAuth and
   jose both support it natively.)
2. **Refresh token rotation policy** — rotate on every use, or only
   after 7 days? Phase 3.x rotates every use (industry standard).
3. **Signing key encryption at rest** — `IDataProtector` is fine for
   v0.1 single-host. Phase 2 swaps to KMS. OK?
4. **`IServiceCollection` extension naming** — `AddSigilApplicationCore`
   / `AddSigilInfrastructureCore` follows the pattern from
   `Plexor.Shared.Persistence`. OK?

## What I won't touch

- `ICurrentUser` / `HttpContextCurrentUser` — already shipped
- `IdentityClaims` constants — already shipped
- `Email` / `PasswordHash` value objects — already shipped
- `IdentityException` codes — already shipped
- Database migrations — already shipped (Phase 2b)
- `Role`, `RoleBinding`, `User` entities — already shipped
- The audit + API key translation flow — deferred