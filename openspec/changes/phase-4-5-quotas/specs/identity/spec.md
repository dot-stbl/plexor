# Spec delta: identity (phase-4-5-quotas)

This file is the additive delta for the identity capability
introduced by the change `openspec/changes/phase-4-5-quotas/`.
Existing identity Requirements are unchanged — only new
requirements are added.

When the change lands, this delta is promoted into
`openspec/specs/identity/spec.md` under `## Requirements`,
and the `## ADDED Requirements` heading is removed.

## ADDED Requirements

### Requirement: `quotas.read` permission

The system SHALL expose a new permission string `quotas.read`
in the role permission catalog
(`Plexor.Modules.Sigil.Domain.Entities.Role.Permissions`).
The permission SHALL be granted to **all authenticated
users** by default — the Migrator's role-assignment default
for the built-in `viewer` role SHALL include `quotas.read`,
and any user-bound role SHALL be able to claim the
permission through normal `[RequirePermission]`
authorization.

`quotas.read` SHALL gate every read endpoint in the quotas
module (`GET /api/v1/quotas/definitions`,
`GET /api/v1/quotas/assignments`,
`GET /api/v1/quotas/usage`,
`GET /api/v1/quotas/effective`).

Missing permission on a quotas read endpoint SHALL return
HTTP 403 with `code = "identity.permission.denied"`.

### Requirement: `quotas.assign.org` permission

The system SHALL expose a new permission string
`quotas.assign.org` in the role permission catalog. The
permission SHALL be granted to the built-in `admin` role by
default (the Migrator SHALL include `quotas.assign.org` in
the `admin` role's permissions during seed).

`quotas.assign.org` SHALL gate every write endpoint in the
quotas module (`PUT /api/v1/quotas/assignments`,
`DELETE /api/v1/quotas/assignments/{id}`).

Missing permission on a quotas write endpoint SHALL return
HTTP 403 with `code = "identity.permission.denied"`.

The `quotas.assign.org` permission is org-scoped only in v1.
Team-scoped (`quotas.assign.team`) and folder-scoped
(`quotas.assign.folder`) variants SHALL be added in Phase 2
when the Team and Folder admin roles land. v1 does not
emit those permission strings.

### Requirement: Tenant-scoped quotas endpoints

The system SHALL enforce tenant isolation on every quotas
endpoint. A user authenticated in Org X SHALL NOT view or
assign quotas for Org Y.

Enforcement:

- Every quotas controller method reads `current.OrgId` from
  the injected `ICurrentUser` (`Plexor.Modules.Sigil.Application.Abstractions.ICurrentUser`)
  and SHALL filter every query by `OrgId = current.OrgId`.
- A request that targets an `OrgId` not equal to
  `current.OrgId` (e.g. via a `?orgId=Y` query parameter)
  SHALL return HTTP 404 — never 403, never 200 with empty
  results. Returning 403 would leak the existence of the
  target org.
- A `ScopeId` that resolves to a Realm entity outside
  `current.OrgId` SHALL also return HTTP 404.

The tenant filter MUST be applied at the controller layer
and at the database query layer (defense in depth); the
EF Core global query filter on `OrgId` is the database-side
enforcement.

### Requirement: Standard 401 / 403 contract

The system SHALL apply the standard authentication /
authorization contract on every quotas endpoint, matching
every other Plexor endpoint:

- **No bearer / invalid bearer** → HTTP 401 with
  `code = "identity.token.expired"` (or the
  appropriate token-error code from the Sigil capability).
- **Valid bearer, missing permission** → HTTP 403 with
  `code = "identity.permission.denied"`.
- **Valid bearer, valid permission, wrong tenant** → HTTP
  404 (see tenant scoping requirement above).

The `code` value SHALL be drawn from the identity capability's
stable-code namespace. Clients MUST branch on `code`, never
on `message`.