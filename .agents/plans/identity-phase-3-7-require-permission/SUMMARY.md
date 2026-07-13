---
phase: 3.7
plan: phase-3-7-require-permission
title: "RequirePermissionAttribute + dynamic permission policies"
status: complete
duration: "~50m"
started: 2026-07-13T08:04:00Z
completed: 2026-07-13T08:55:00Z
tasks_completed: 5
files_modified: 9
tags: [sigil, auth, permissions, authorization, shared]
key-files:
  created:
    - src/shared/Plexor.Shared.Authorization/Plexor.Shared.Authorization.csproj
    - src/shared/Plexor.Shared.Authorization/AuthorizationClaimNames.cs
    - src/shared/Plexor.Shared.Authorization/AuthorizationPolicyNames.cs
    - src/shared/Plexor.Shared.Authorization/PermissionRequirement.cs
    - src/shared/Plexor.Shared.Authorization/PermissionAuthorizationHandler.cs
    - src/shared/Plexor.Shared.Authorization/PermissionPolicyProvider.cs
    - src/shared/Plexor.Shared.Authorization/RequirePermissionAttribute.cs
    - src/shared/Plexor.Shared.Authorization/PlexorSharedAuthorizationServiceCollectionExtensions.cs
    - tests/unit/Plexor.Shared.Authorization.Unit/Plexor.Shared.Authorization.Unit.csproj
    - tests/unit/Plexor.Shared.Authorization.Unit/PermissionRequirementShould.cs
    - tests/unit/Plexor.Shared.Authorization.Unit/PermissionPolicyProviderShould.cs
    - tests/unit/Plexor.Shared.Authorization.Unit/RequirePermissionAttributeShould.cs
  modified:
    - plexor.slnx
    - src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Plexor.Modules.Sigil.Infrastructure.csproj
    - src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Installers/SigilInfrastructureInstaller.cs
key-decisions:
  - "Plexor.Shared.Authorization lives in shared/ (not in Sigil) so controllers in any module can use [RequirePermission] without taking on a module-specific dependency."
  - "Permission comparison is case-insensitive (OrdinalIgnoreCase) — matches AWS / GCP / YC IAM conventions and prevents silent auth failures from case-mismatched claim values."
  - "AND semantics across multiple permissions via the handler iterating PendingRequirements — handler call is per-requirement, but a single failure still fails the request."
  - "Encoding multiple permissions as 'permission:foo,bar' on one policy string keeps the attribute a single line; multi-attribute [RequirePermission] also works via AllowMultiple = true."
  - "AuthorizationClaimNames mirrors Sigil.IdentityClaims.Permission — duplicate constant, doc note flagging the sync invariant, no runtime cross-dependency."
  - "Dynamic IAuthorizationPolicyProvider instead of pre-registered policies — permission names surface from attribute at runtime, no central registry to keep in sync."
requirements-completed:
  - "[RequirePermission] attribute supported on controllers and actions with params string[] permissions"
  - "AND semantics across multiple required permissions"
  - "Dynamic IAuthorizationPolicyProvider resolves 'permission:*' names on demand"
  - "Non-permission policy names fall through (provider returns null)"
  - "Sigil infrastructure registers the policy provider + handler via AddPlexorAuthorization()"
---

# Phase 3.7 Plan: RequirePermission Attribute + Dynamic Permission Policies — Summary

The Plexor authorization pipeline now supports permission-based
gating as a first-class cross-cutting concern. Controllers in any
module can write `[RequirePermission("vms.read")]` (or
multi-permission comma-separated) and the dynamic policy provider
resolves it at request time without any per-project DI plumbing.

## Duration ~50m (2026-07-13T08:04:00Z → 2026-07-13T08:55:00Z)

## Tasks

- **Task 1** — `Plexor.Shared.Authorization` project scaffold + base
  types (AuthorizationClaimNames, AuthorizationPolicyNames,
  PermissionRequirement) (commit `cdea82b`)
- **Task 2** — Handler + policy provider + attribute + DI extension
  (commit `cdea82b`)
- **Task 3** — Unit tests for handler, provider, and attribute
  (commit `cdea82b`)
- **Task 4** — Wire into SigilInfrastructureInstaller + csproj
  ProjectReference (commit `cdea82b`)
- **Task 5** — slnx reference for new project + test project
  (commit `cdea82b`)

## Deviations from Plan

**None — plan executed exactly as written.**

Two notable interactions with the branch state that needed pre-commit
cleanup (not behavioural deviations):

- The branch had been scaffolded off a stale tree that referenced
  pre-rename module names (Tenants / Billing / Audit / Identity) and
  the old `src/host/Plexor.NodeAgent/` path; the actual filesystem
  contained only Sigil/Realm + lowercase `src/providers/substrate/`.
  Resolved by `git checkout HEAD -- plexor.slnx` (correct version is
  at HEAD already) and a single-line addition of the new
  `Plexor.Shared.Authorization.csproj` reference.

**Total deviations:** 0 auto-fixed (Rules 1–3). **Out-of-scope:** 0. **Escalated:** 0.

## Authentication Gates
None.

## Out-of-Scope Issues
- `IdentityException → ProblemDetails` mapping is still on the
  Phase 4 punch list. Phase 3.7 only wires the permission check
  — exception-to-status mapping for `IdentityException` codes lands
  when the auth controllers (`/auth/login`, `/auth/refresh`) come up.
- `IAuthorizeData.AuthenticationSchemes` is a settable property on the
  public interface; the attribute exposes `set;` (not `init;`) to
  satisfy the interface contract. Tests cover the constructor
  pathway; runtime setters are not used by any code in this plan.

## Verification

- `dotnet build plexor.slnx -c Debug` — **0 warnings, 0 errors**
  (9.36s).
- `dotnet test tests/unit/Plexor.Shared.Authorization.Unit` —
  **17/17 passed** in 24ms:
  - PermissionAuthorizationHandlerShould: 6 facts
  - PermissionPolicyProviderShould: 6 facts
  - RequirePermissionAttributeShould: 5 facts

## Files Touched

- **Created: 14**
  - `src/shared/Plexor.Shared.Authorization/{Plexor.Shared.Authorization.csproj, AuthorizationClaimNames.cs, AuthorizationPolicyNames.cs, PermissionRequirement.cs, PermissionAuthorizationHandler.cs, PermissionPolicyProvider.cs, RequirePermissionAttribute.cs, PlexorSharedAuthorizationServiceCollectionExtensions.cs}`
  - `tests/unit/Plexor.Shared.Authorization.Unit/{Plexor.Shared.Authorization.Unit.csproj, PermissionRequirementShould.cs, PermissionPolicyProviderShould.cs, RequirePermissionAttributeShould.cs}`

- **Modified: 3**
  - `plexor.slnx` — added the two new csproj references
  - `src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Plexor.Modules.Sigil.Infrastructure.csproj` — added the ProjectReference
  - `src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Installers/SigilInfrastructureInstaller.cs` — added `services.AddPlexorAuthorization()`

## Next

Phase 3.7 makes the authorization gate available. Phase 4 can now
build:

- Auth controllers (`/auth/login`, `/auth/refresh`, `/auth/me`) that
  issue tokens bearing `permission` claims — those claims power the
  `[RequirePermission]` gate today.
- Identity-domain `IExceptionHandler` mapping
  `IdentityException.Code` → status + `ProblemDetails.type`.
- CRUD controllers under `@[RequirePermission]` for things like
  `/vms`, `/clusters`, `/users`, `/iam`.

Plan a single Phase 4 plan covering controllers + the
identity-exception handler, or split it into plan-3.7a
(auth controllers only, simplest scope) + plan-3.7b (identity
exception handler). Up to the user's preference.
