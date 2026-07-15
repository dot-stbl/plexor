# Plan: dual auth-provider layer (Plexor Sigil + external OIDC)

## Goal

Make the Sigil (Identity) module **pluggable** between two
authentication backends per **tenant** (a.k.a. organization, the
`orgs` row in the `realm` schema):

1. **`sigil`** (default) — the existing email + password flow against
   `sigil.users.password_hash`. No config required; every fresh
   tenant starts here. The `IdentityBootstrapper` admin user lives
   on this backend.

2. **`oidc`** (optional) — the tenant's admin points Plexor at an
   external OIDC provider (Keycloak, Authentik, Dex, Google
   Workspace, Azure AD, etc.) via the standard **Authorization
   Code + PKCE** flow. Plexor is a resource server only —
   it validates the OIDC-issued ID token, derives a Plexor access
   token bound to the tenant, and treats subsequent calls like any
   other Sigil-issued bearer.

The two backends are **never mixed per request** — a tenant is
either "Sigil-only" (no external IDP configured) or "OIDC-only"
(an external IDP is configured and Sigil is the local break-glass
admin path only). A future extension can layer "both available"
but the v0.1 model is exactly one of the two.

## Why this plan is a separate worktree

`plan/auth-providers` is **upstream of the MVP deploy** (per
`plan/mvp-deploy`) and **downstream of Phase 4 Sigil identity
work**. The MVP runbook's current draft assumes "Keycloak is the
IDP" — that's wrong: the MVP default is "Sigil is the IDP", and
Keycloak is a *configurable swap-in*. Without this plan, the
MVP runbook ships with the wrong mental model and every operator
who picks Plexor instead of Keycloak has to figure out the dual-IDP
story from scratch.

## Architectural context (from .agents/docs)

- **Plexor as cloud** (`scope.md` "Plexor стартует с 8-10 core
  сервисов"): the cloud's user model is *multi-tenant* (a tenant
  is an organization, not a single user) — the operator may
  self-host for personal use today, but the architecture must
  support the SaaS use case where many tenants share one deploy.
  Single-tenant MVP is the *default*; multi-tenant is the *shape*
  the data model has to support from day one.
- **Provider model** (`providers.md`): the boundary between what
  Plexor ships vs. what the user brings. Identity is a **user-side
  provider** — Plexor ships a default (Sigil), the user can swap it
  for their existing corporate SSO. Phase 4 ships the default;
  this plan adds the swap.
- **Identity** (`architecture/identity.md`): OIDC resource-server
  shape. The bearer handler already validates JWTs against
  configured `Issuer` + `SigningKey`; this plan extends that to
  multiple issuers (one per tenant's auth provider).
- **Resource-server discipline** (`architecture/identity.md` §"Plexor
  as resource server"): the bearer handler treats the token as
  opaque — it does NOT know what's inside. The OIDC external-tenant
  path follows the same shape: external IDP issues, Plexor
  validates signature, claims become principal.

## Aggregate shape

### `OrgAuthProviderConfig` (Plexor.Modules.Realm.Domain.Entities.OrgAuthProviderConfig)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` (UUID v7) | Surrogate. |
| `OrgId` | `Guid` | FK to the `realm.orgs.id` row. UNIQUE — exactly one config per org. |
| `Provider` | enum `OrgAuthProvider` | `Sigil` (default) or `Oidc`. |
| `OidcAuthority` | `string?` | OIDC issuer URL — `https://keycloak.plexor.example.com/realms/plexor`. Null for Sigil. |
| `OidcClientId` | `string?` | Confidential client id registered at the OIDC provider. Null for Sigil. |
| `OidcClientSecret` | `string?` (encrypted-at-rest in production) | Confidential client secret. Null for Sigil. |
| `OidcScopes` | `IReadOnlyList<string>` (text[]) | Default `["openid", "profile", "email"]`. Configurable per tenant. |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | `ICreatedAt` / `IUpdatedAt`. |

Invariants:
- Exactly **one** row per `OrgId` (enforced by UNIQUE index).
- `OidcAuthority` is non-null iff `Provider = Oidc`; same for client
  id / secret. `Provider = Sigil` rows have all three OIDC fields
  null.
- `OidcAuthority` is a URL — must be absolute, must be HTTPS in
  production. Validated on PUT.

## Application services

```csharp
public interface IAuthProvider
{
    /// <summary>Discriminator string — "sigil" or "oidc".</summary>
    public string ProviderId { get; }

    /// <summary>True if the provider can serve the given tenant
    /// (always true; controls live in OrgAuthProviderConfig).</summary>
    public bool CanAuthenticateFor(OrgAuthProviderConfig config);

    /// <summary>Resolve the org + subject for a freshly-issued
    /// bearer token. Returns null if the token doesn't belong to
    /// this provider or the org's config doesn't match.</summary>
    public Task<AuthResolution?> ResolveAsync(
        string rawToken,
        CancellationToken cancellationToken = default);
}

/// <summary>Resolution of a bearer token to a specific org +
/// subject. The bearer handler uses this to populate the principal's
/// user-id / tenant-id claims before passing it to the authorization
/// pipeline.</summary>
public sealed record AuthResolution(
    Guid OrgId,
    Guid UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
```

Two implementations:

### `SigilAuthProvider` (default)

- Wraps the existing `IPasswordHasher` + `LoginCommand` /
  `RefreshCommand` paths. The bearer handler doesn't call this
  directly — Sigil's login endpoint is still
  `POST /auth/login`, refresh is `POST /auth/refresh`, and
  `VerifyAsync` on the `IJwtSigningService` is unchanged. The
  provider here exists so the bearer handler has a single
  abstraction to consult ("who issued this token? which sigil
  user / org does it correspond to?") — the Sigil path returns the
  resolution directly from the token's `sub` + `tid` claims.

### `ExternalOidcAuthProvider`

- Verifies an external IDP-issued JWT against the tenant's
  configured `OidcAuthority` (issuer URL) and the `OidcClientId`
  (audience). Uses the same `TokenValidationParameters` shape as
  Sigil's `JwtSigningService.VerifyAsync` but parameterized by the
  per-tenant config:
  - `ValidIssuer` = the tenant's `OidcAuthority`
  - `ValidAudience` = the tenant's `OidcClientId`
- `kid` lookup: external keys come from the OIDC provider's JWKS
  endpoint. We fetch + cache the JWKS for the configured authority
  on first use and refresh on `kid` miss.
- Returns `AuthResolution` with:
  - `OrgId` = the tenant whose `OidcClientId` matches the `aud`
    claim
  - `UserId` = the `sub` claim (deterministic GUID v5 of the OIDC
    issuer + sub — so the same external user maps to the same
    Plexor user id across logins)
  - `Roles` = the `realm_access.roles` claim (Keycloak) or
    `roles` claim (generic)
  - `Permissions` = derived from roles via `IPermissionResolver`

### Auth resolution dispatcher

The bearer handler routes by `iss` claim: the issuer URL maps
to an `OrgAuthProviderConfig` row, which picks the provider. A
small in-memory cache (`IMemoryCache`) keyed on
`(iss, kid, audience)` keeps the dispatch hot.

## OIDC flow endpoints (per tenant, behind `OrgAuthProviderConfig`)

| Verb | Path | Notes |
|---|---|---|
| `GET`  | `/auth/oidc/authorize?org={slug}` | Builds the authorization URL with PKCE challenge + `state` (CSRF). Redirects to the tenant's OIDC authority. |
| `GET`  | `/auth/oidc/callback?code=...&state=...` | Validates `state` against the server-side cache, exchanges the code for tokens at the OIDC provider, extracts `id_token` claims, mints a Plexor access token bound to the tenant. |
| `GET`  | `/auth/oidc/logout?org={slug}` | RP-initiated logout (OIDC end_session_endpoint). Optional — many operators don't bother. |

All three endpoints are **anonymous** (no bearer required) — the
whole point is the inbound flow. They sit under `/api/v1/auth/oidc/*`.

## Endpoint changes to existing auth

| Path | Old behaviour | New behaviour |
|---|---|---|
| `POST /auth/login` | Always Sigil (email + password) | **400** with `identity.credentials.provider_mismatch` if the tenant's `OrgAuthProvider != Sigil`. Directs the user to `/auth/oidc/authorize` instead. |
| `POST /auth/refresh` | Always Sigil | Refresh is bound to the token's `iss` — Sigil-issued tokens refresh at the Sigil endpoint, OIDC-issued tokens re-mint at the OIDC provider (or refresh at Plexor with the OIDC refresh token, depending on the tenant config). v0.1: both flows refresh at Plexor. |
| `GET /auth/me` | Sigil-issued token only | Works for any valid bearer — the resolution flow turns an OIDC `sub` into a Plexor user id. |

## REST surface (org admin)

| Verb | Path | Permission | Notes |
|---|---|---|---|
| `GET`    | `/api/v1/iam/orgs/{orgId}/auth-provider` | `org.auth.read` | Returns the current config (OIDC fields are redacted). |
| `PUT`    | `/api/v1/iam/orgs/{orgId}/auth-provider` | `org.auth.update` | Toggle `provider` + set OIDC fields. Validates the OIDC authority URL (HTTPS, absolute). |
| `POST`   | `/api/v1/iam/orgs/{orgId}/auth-provider/test` | `org.auth.update` | Test the OIDC connection: PKCE handshake + id_token claim validation against the configured authority. Returns the resolved claims for inspection. |

## Persistence

- `OrgAuthProviderConfig` table in the **`realm`** schema (the
  realm module is the natural owner — tenants live there).
  Migration is a single `tool ef migrations add InitAuthProviders`
  generated against `Plexor.Modules.Realm.Infrastructure`.
- Index: `UNIQUE (org_id)` — exactly one config per org.
- The `OIDC client secret` is encrypted at rest via the existing
  `Plexor.Shared.Security.IDataProtector` (the same infrastructure
  Keycloak-bind uses for refresh-token hashing in Phase 4).

## Cross-cutting

- The bearer handler gets a thin `IAuthProviderResolver` (or
  `IAuthProviderRegistry`) injected that:
  1. Extracts the `iss` claim from the bearer.
  2. Looks up the matching `OrgAuthProviderConfig` (cached).
  3. Picks the right `IAuthProvider` (Sigil / OIDC) and dispatches
     the resolution.
  4. The principal is built from `AuthResolution` — the existing
     bearer pipeline (claims → `HttpContext.User`) doesn't change.
- New audit event: `OrgAuthProviderChanged(orgId, oldProvider,
  newProvider, whoChangedIt)` — emitted to NATS for the `atlas`
  module to pick up later. Also written to `ILogger.LogInformation`
  in v0.1.

## Build order

1. `OrgAuthProviderConfig` entity + migration
   `InitAuthProviders` (tool-generated).
2. `IAuthProvider` + `AuthResolution` in `Sigil.Application`.
3. `SigilAuthProvider` (trivial — wraps existing pipeline).
4. `ExternalOidcAuthProvider` — JWKS fetch + cache +
   `TokenValidationParameters` configured from per-tenant config.
5. `IAuthProviderResolver` — the dispatch cache; bearer handler
   uses it.
6. OIDC flow endpoints (`/auth/oidc/authorize`, `/callback`,
   `/logout`) — uses `Microsoft.AspNetCore.Authentication.OpenIdConnect`
   under the hood; we don't use its middleware pipeline, just
   its primitives (`Pkce.Generate`, `TokenValidationParameters`,
   `JsonWebTokenHandler`).
7. Org admin REST endpoints (`GET` / `PUT` / `POST test`).
8. `/auth/login` and `/auth/refresh` updates — guard on the
   tenant's `OrgAuthProvider`, redirect to OIDC if not Sigil.
9. Tests: `SigilAuthProvider` (trivial), `ExternalOidcAuthProvider`
   (against `IdentityServer` testcontainer or `openiddict-dotnet`
   in-memory), bearer handler dispatcher
   (Sigiltoken vs OIDC token → correct `OrgId`).

## Acceptance

- A tenant with `Provider = Sigil` (default, set by
  `OrgAuthProviderInit`) logs in via `POST /auth/login` and
  receives a Plexor access token. Bearer handler resolves it to
  the correct org via the `tid` claim.
- An admin runs `PUT /iam/orgs/{id}/auth-provider` with
  `provider: oidc, oidcAuthority: https://keycloak.../realms/...`.
  The next login attempt is redirected to Keycloak via
  `/auth/oidc/authorize`. On callback, Plexor validates the
  Keycloak-issued `id_token` against the configured authority and
  issues a Plexor access token.
- A bearer for an OIDC-issued token, validated via the
  `ExternalOidcAuthProvider`, populates `HttpContext.User` with the
  same shape as a Sigil-issued token — `[RequirePermission(...)]`
  attributes work unchanged.
- `dotnet build plexor.slnx -c Debug` clean; tests cover both
  branches; OpenAPI auto-emits 401/403/500.

## Out of scope (Phase 5+ or later)

- **Multi-IdP per tenant** (a tenant uses Sigil for admins and
  OIDC for end users) — single provider per tenant in v0.1.
- **OIDC user provisioning via SCIM** — Phase 5+, after the
  `realm.tenants` and `identity.users` tables have SCIM endpoints.
- **Just-in-time role mapping** — `Role.Name = "keycloak_role_x"`
  patterns are v0.2+.
- **Per-org certificate pinning for the OIDC authority** — not
  needed for `https`; `JwtSecurityTokenHandler` validates the chain
  against the system trust store by default.
