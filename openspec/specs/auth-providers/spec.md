# Capability: auth-providers

## Purpose

Per-tenant authentication backend selection. The current Sigil
identity provider (`openspec/specs/identity/spec.md`) ships
email + password + JWT as the default for every organization. A
multi-tenant SaaS deploy with enterprise tenants needs the
option to point a tenant at an external OIDC identity provider
(Keycloak, Authentik, Azure AD, Google Workspace, etc.) instead
of maintaining local credentials.

This capability models the per-tenant choice between "local
Sigil" and "external OIDC". The two backends are **never mixed
per request**: a tenant is either Sigil-only (no external IDP
configured) or OIDC-only (an external IDP is configured and
Sigil remains the local break-glass admin path).

The auth-providers capability is **planned for Phase 4.6+ and
is not implemented in v0.1**. The current state is: every tenant
authenticates through Sigil.

The architectural design for this capability is in
`.agents/docs/plans/plan-auth-providers.md` — that plan documents
the `OrgAuthProviderConfig` entity, the `IAuthProvider`
abstraction, the `SigilAuthProvider` and `ExternalOidcAuthProvider`
implementations, the OIDC flow endpoints (`/auth/oidc/authorize`,
`/auth/oidc/callback`, `/auth/oidc/logout`), and the per-tenant
admin REST endpoints (`GET` / `PUT` / `POST test` on
`/api/v1/iam/orgs/{orgId}/auth-provider`).

## Requirements

See `changes/` for the proposed implementation (not yet proposed;
plan lives in `.agents/docs/plans/plan-auth-providers.md`).

No current-state requirements exist for this capability. Once a
proposal is filed under `openspec/changes/<change-id>/`, the
`## ADDED Requirements` from that change's
`specs/auth-providers/spec.md` will move up into this file
when the change is merged.