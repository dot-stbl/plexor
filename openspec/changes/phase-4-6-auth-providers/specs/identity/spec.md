# Spec delta: identity (phase-4-6-auth-providers)

This file is the additive delta for the identity capability
introduced by the change `openspec/changes/phase-4-6-auth-providers/`.
Existing identity Requirements are unchanged — only new
requirements are added.

When the change lands, this delta is promoted into
`openspec/specs/identity/spec.md` under `## Requirements`,
and the `## ADDED Requirements` heading is removed.

## ADDED Requirements

### Requirement: `org.auth.read` permission

The system SHALL expose a new permission string `org.auth.read`
in the role permission catalog
(`Plexor.Modules.Sigil.Domain.Entities.Role.Permissions`).
The permission SHALL be granted to the built-in `admin`
role by default — the built-in `admin` role already carries
the `*` wildcard minted by
`Plexor.Migrator/IdentityBootstrapper`, which covers every
permission (including `org.auth.read`). v1 does not grant
`org.auth.read` to the built-in `viewer` role.

`org.auth.read` SHALL gate the read endpoint in the
auth-providers module (`GET
/api/v1/iam/orgs/{orgId}/auth-provider`).

Missing permission on the auth-providers read endpoint SHALL
return HTTP 403 with `code = "identity.permission.denied"`.

### Requirement: `org.auth.update` permission

The system SHALL expose a new permission string
`org.auth.update` in the role permission catalog. The
permission SHALL be granted to the built-in `admin` role by
default (the built-in `admin` role's `*` wildcard covers
it).

`org.auth.update` SHALL gate the write endpoints in the
auth-providers module
(`PUT /api/v1/iam/orgs/{orgId}/auth-provider` and
`POST /api/v1/iam/orgs/{orgId}/auth-provider/test`).

Missing permission on the auth-providers write endpoints
SHALL return HTTP 403 with `code = "identity.permission.denied"`.

### Requirement: Tenant-scoped auth-provider endpoints

The system SHALL enforce tenant isolation on every
auth-provider endpoint. A user authenticated in Org X
SHALL NOT view or mutate Org Y's auth-provider config.

Enforcement:

- Every auth-provider controller method reads
  `currentUser.TenantId` from the injected `ICurrentUser`
  (`Plexor.Modules.Sigil.Application.Abstractions.ICurrentUser`)
  and SHALL filter every query by `OrgId = currentUser.TenantId`.
- A request that targets an `OrgId` not equal to
  `currentUser.TenantId` (via the URL path parameter)
  SHALL return HTTP 404 — never 403, never 200 with empty
  results. Returning 403 would leak the existence of the
  target org.

The tenant filter MUST be applied at the controller layer;
the EF Core global query filter on `OrgId` (when added in a
future phase) would be the database-side enforcement.

### Requirement: Standard 401 / 403 / 404 contract

The system SHALL apply the standard authentication /
authorization / tenant-isolation contract on every
auth-provider endpoint, matching every other Plexor endpoint:

- **No bearer / invalid bearer** → HTTP 401 with
  `code = "identity.token.expired"` (or the appropriate
  token-error code from the Sigil capability).
- **Valid bearer, missing permission** → HTTP 403 with
  `code = "identity.permission.denied"`.
- **Valid bearer, valid permission, wrong tenant** → HTTP
  404 (see tenant scoping requirement above).

The `code` value SHALL be drawn from the identity capability's
stable-code namespace. Clients MUST branch on `code`, never
on `message`.
