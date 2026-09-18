# Tasks: theme-marketplace-backend

Numbered checklist.

## TMB.1 — ThemeInstallation entity + migration

- [x] `ThemeInstallation` entity in
      `Plexor.Modules.Branding.Domain.Entities.ThemeInstallation`.
- [x] EF DbContext (`BrandingDbContext`) already exists;
      add `ThemeInstallationConfiguration` (snake_case,
      schema `branding`).
- [x] Migration `InitThemeInstallations` via
      `dotnet ef migrations add InitThemeInstallations --context BrandingDbContext`.
- [x] Index on `(org_id, activated_at DESC)`.

## TMB.2 — HMAC manifest verifier

- [x] `ThemeManifestVerifier` in
      `Plexor.Modules.Branding.Infrastructure`.
- [x] `ThemeManifestSigningOptions` class — `SigningKey`,
      `KeyId` (env-bound).
- [x] `Verify(manifest, signature)` — HMAC-SHA256 hex
      compare (constant-time).
- [x] Unit tests: `ThemeManifestVerifierShould` (5 cases —
      happy path, missing key, bad signature, signature
      mismatch, canonicalization).

## TMB.3 — REST endpoints

- [x] `ThemeInstallationsController` with the 5 endpoints.
- [x] `GET /api/v1/branding/themes/installations`
      (`branding.read`).
- [x] `POST /api/v1/branding/themes/installations`
      (`branding.update`) — body:
      `{ manifest, signature }`.
- [x] `DELETE /api/v1/branding/themes/installations/{themeId}`
      (`branding.update`).
- [x] `PUT /api/v1/branding/themes/installations/{themeId}/activate`
      (`branding.update`).
- [x] `DELETE /api/v1/branding/themes/installations/active`
      (`branding.update`).

## TMB.4 — PUT simplification

- [x] PUT body is `{ themeId : string }` only — the host
      loads the manifest from the registry, signs it
      internally if the theme is built-in, verifies the
      signature for community themes.
- [x] The POST `install` endpoint is unchanged (the
      manifest + signature come from the operator).

## TMB.5 — Audit emission

- [x] `theme.installed.installed` on POST install.
- [x] `theme.installed.activated` on PUT activate.
- [x] `theme.installed.deactivated` on DELETE active.
- [x] `theme.installed.uninstalled` on DELETE
      installation.

## TMB.6 — FE wiring

- [x] Replace `useInstalledThemes`' localStorage
      implementation with API calls
      (`kubb`-generated hooks).
- [x] `useActiveTheme` hook calls
      `GET .../installations` on mount + after mutations.
- [x] Optimistic updates on `install` / `activate` /
      `deactivate` / `uninstall`.

## TMB.7 — Merge

- [x] `merge theme-marketplace-backend` commit lands on
      `main`.
- [x] `merge theme-audit-trail` follow-up commit lands on
      `main` (the audit events above).
