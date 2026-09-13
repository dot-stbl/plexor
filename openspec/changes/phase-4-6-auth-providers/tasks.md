# Tasks: phase-4-6-auth-providers

Numbered checklist. Each sub-section (`4.6.1` through `4.6.3`) is
one or more commits, each independently buildable against the
current state of `plexor.slnx`.

## 4.6.1 — Configuration layer (this change)

- [x] Add `OrgAuthProviderConfig` entity +
      `OrgAuthProvider` enum in
      `Plexor.Modules.Realm.Domain.Entities`.
- [x] Add `Tables.OrgAuthProviderConfigs =
      "org_auth_provider_configs"` to
      `Plexor.Shared.Persistence.DatabaseInformation`.
- [x] Wire `OrgAuthProviderConfig` in `RealmDbContext`
      (DbSet + `OrgAuthProviderConfigConfiguration` —
      snake_case, `text[]` for `OidcScopes`, UNIQUE on
      `OrgId`).
- [x] EF migration `InitAuthProviders` via
      `dotnet ef migrations add InitAuthProviders --context RealmDbContext`.
- [x] Add `AuthProviderPermissions` (constants
      `Read = "org.auth.read"`, `Update = "org.auth.update"`,
      `AdminWildcard = "*"`) in
      `Plexor.Shared.Kernel.AuthProviders`.
- [x] Add `IOrgAuthProviderSeeder` (Application) +
      `EfOrgAuthProviderSeeder` (Infrastructure, internal)
      + `OrgAuthProviderSeeder` (IHostedService, Application)
      + `RealmAuthProvidersInstaller` extension.
- [x] Register the installer in both `Plexor.Host/Program.cs`
      and `Plexor.Migrator/Program.cs`.
- [x] Wire `AddDataProtection().PersistKeysToFileSystem(...)`
      in `Plexor.Host/Program.cs` with the keyring rooted at
      `<data-root>/dataprotection-keys/`.
- [x] Add DTOs (`OrgAuthProviderConfigResponse`,
      `UpsertOrgAuthProviderRequest`,
      `OrgAuthProviderTestResult`).
- [x] Add `UpsertOrgAuthProviderRequestValidator` (case-
      insensitive provider, HTTPS-only authority, OIDC
      secret minimum length, non-empty scopes).
- [x] Add `OrgAuthProvidersControllerHelpers` (file-static —
      ProblemDetails constructors, entity → response
      projection with `OidcClientSecretMasked`,
      `BuildDiscoveryDocumentUrl`, `FetchDiscoveryAsync`).
- [x] Add `OrgAuthProvidersController` (`GET` / `PUT` /
      `POST test`) using `ExecuteUpdateAsync` for the
      in-place updates (entity uses init-only properties).
- [x] Wire the FluentValidation validator +
      `IDataProtectionProvider` +
      `IHttpClientFactory("Plexor-OidcDiscovery")` in
      `Plexor.Host/Program.cs`.
- [x] Add unit tests:
      `EfOrgAuthProviderSeederShould` (4 cases against
      in-memory `RealmDbContext`) +
      `UpsertOrgAuthProviderRequestValidatorShould` (9 cases
      against `UpsertOrgAuthProviderRequestValidator`).

## 4.6.2 — Auth-provider resolver + bearer handler (follow-up)

- [ ] `IAuthProvider` interface + `AuthResolution` discriminated
      result in `Plexor.Modules.Sigil.Application.Auth`.
- [ ] `SigilAuthProvider` — trivial wrapper over the existing
      `IJwtSigningService` / `IPasswordHasher` paths.
- [ ] `ExternalOidcAuthProvider` — JWKS fetch + cache +
      `TokenValidationParameters` configured from the per-tenant
      `OrgAuthProviderConfig`.
- [ ] `IAuthProviderResolver` — the in-memory
      `(iss, kid, audience)` cache that the bearer handler
      consults.
- [ ] `BearerAuthenticationHandler` updated to dispatch via
      the resolver (drop the Sigil-only assumption).
- [ ] Unit tests for both providers + the dispatcher.

## 4.6.3 — OIDC flow endpoints (follow-up)

- [ ] `GET /auth/oidc/authorize?org={slug}` — build the
      authorization URL with PKCE challenge + state.
- [ ] `GET /auth/oidc/callback?code=...&state=...` —
      validate state, exchange code, extract claims, mint a
      Plexor access token.
- [ ] `GET /auth/oidc/logout?org={slug}` — RP-initiated
      logout (best-effort).
- [ ] `POST /auth/login` updated — 400 with
      `identity.credentials.provider_mismatch` when the tenant's
      `OrgAuthProvider != Sigil`.
- [ ] `POST /auth/refresh` updated — refresh bound to the
      token's `iss` claim (Sigil refresh vs OIDC refresh).
- [ ] Integration tests against `IdentityServer` /
      `openiddict-dotnet` Testcontainer.
