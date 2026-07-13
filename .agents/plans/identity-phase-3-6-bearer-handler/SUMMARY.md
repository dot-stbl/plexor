---
phase: 3.6
plan: phase-3-6-bearer-handler
title: "BearerAuthenticationHandler for ASP.NET Core"
status: complete
duration: "~25m"
started: 2026-07-13T07:00:00Z
completed: 2026-07-13T07:25:00Z
tasks_completed: 3
files_modified: 6
tags: [sigil, auth, jwt, aspnet-core]
key-files:
  created:
    - src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Auth/BearerAuthenticationHandler.cs
    - src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Auth/BearerOptions.cs
    - tests/unit/Plexor.Modules.Sigil.Unit/Auth/BearerAuthenticationHandlerShould.cs
  modified:
    - src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Plexor.Modules.Sigil.Infrastructure.csproj
    - src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Installers/SigilInfrastructureInstaller.cs
    - tests/unit/Plexor.Modules.Sigil.Unit/Plexor.Modules.Sigil.Unit.csproj
key-decisions:
  - "Microsoft.NET.Sdk + <FrameworkReference Include=\"Microsoft.AspNetCore.App\" /> instead of Microsoft.NET.Sdk.Web so the library remains non-executable (Web SDK requires Main)"
  - "BearerOptions is its own type, not the framework's BearerOptions from Microsoft.AspNetCore.Authentication.Bearer — we don't use TokenValidationParameters, we delegate to IJwtSigningService"
  - "BearerOptions.Realm is a settable string defaulting to \"plexor\" — overridable in appsettings.json later via Authentication:Bearer:Realm"
  - "AuthenticationHandler<TOptions> + AuthenticateResult.Success(AuthenticationTicket) overload — works on net10.0 (the 2-arg principal+properties overload is no longer present)"
requirements-completed:
  - "JWT verification integrated into ASP.NET Core authentication middleware"
  - "[Authorize] attribute triggers 401 with WWW-Authenticate: Bearer realm=\"plexor\""
  - "AuthenticateResult.Fail with reason for Invalid/Malformed tokens"
  - "AuthenticationTicket carries ClaimsPrincipal from VerifyResult.Success"
---

# Phase 3.6 Plan: BearerAuthenticationHandler — Summary

The Plexor Bearer authentication scheme is now wired into the ASP.NET
Core pipeline. `BearerAuthenticationHandler` reads the `Authorization:
Bearer <jwt>` header, delegates verification to the existing
`IJwtSigningService` (Phase 3.3-3.4), and produces the standard
`AuthenticateResult` outcomes. `AddAuthentication` / `AddAuthorization`
are registered in `AddSigilInfrastructureCore`, so downstream
controllers can use `[Authorize]` without additional plumbing.

## Duration  ~25m (2026-07-13T07:00:00Z → 2026-07-13T07:25:00Z)

## Tasks

- **Task 1** — Add `BearerOptions` + `BearerAuthenticationHandler`
  (commit `0f1e42a`)
- **Task 2** — Wire `AddAuthentication`/`AddAuthorization` and
  `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
  (commit `d84b268`)
- **Task 3** — Add six unit tests in
  `tests/unit/Plexor.Modules.Sigil.Unit/Auth/BearerAuthenticationHandlerShould.cs`
  (commit `84bbc8f`)

## Deviations from Plan

**None of plan-level — but two implementation surprises:**

**[Rule 1 — `AuthenticationMetadata` removed.** Found during: Task 1]
The plan called for `AuthenticateResult.Success(principal, properties,
new AuthenticationMetadata(Scheme.Name))` — the three-argument overload
that takes an `AuthenticationMetadata`. This type does not exist on
`net10.0`'s `AuthenticateResult`. Fix: dropped the `AuthenticationMetadata`
argument and used the `AuthenticateResult.Success(AuthenticationTicket)`
overload instead. `Scheme.Name` is preserved on the ticket. Verification:
`dotnet build` + 6/6 tests pass. Commit: `0f1e42a`.

**[Rule 1 — `Microsoft.NET.Sdk.Web` rejected for non-executable library.**
Found during: Task 2] `Microsoft.NET.Sdk.Web` requires a `Main` entry
point — Infrastructure has none. Fix: kept `Microsoft.NET.Sdk` and
added `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. This
gives the same AspNetCore shared framework without the executable
constraint. Verification: `dotnet build` succeeds. Commit: `d84b268`.

**[Rule 1 — `_ = configuration;` placeholder removed.** Found during:
Task 2] The plan kept `configuration` parameter for future Options
binding, with a `_ = configuration;` discard. This violates
`async-and-tasks.md` §6 (meaningless discard ban — generalised form:
`_ = placeholder_param`). Fix: dropped the placeholder, will reintroduce
the parameter when Options binding actually needs it. Verification:
build clean. Commit: `d84b268`.

**Total deviations:** 3 auto-fixed (Rule 1). **Out-of-scope:** 0. **Escalated:** 0.

## Authentication Gates
None.

## Out-of-Scope Issues
None for this plan. Pre-existing review.sh fails (Plexor.Shared.Console,
Plexor.NodeAgent, Plexor.Shared.Workloads) belong to other branches and
are flagged as `WIP-others-*` stashes — out of scope here.

## Verification
- `dotnet build plexor.slnx -c Debug` — **0 warnings, 0 errors**
  (9.50s).
- `dotnet test tests/unit/Plexor.Modules.Sigil.Unit` — **6/6 passed**
  in 66ms.
- `bash tools/review.sh` — fails on pre-existing WIP files only; the
  four files touched by this plan show no hits.

## Files Touched
- **Created: 3**
  - `src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Auth/BearerAuthenticationHandler.cs`
  - `src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Auth/BearerOptions.cs`
  - `tests/unit/Plexor.Modules.Sigil.Unit/Auth/BearerAuthenticationHandlerShould.cs`
- **Modified: 3**
  - `src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Plexor.Modules.Sigil.Infrastructure.csproj` (+1 FrameworkReference)
  - `src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Installers/SigilInfrastructureInstaller.cs` (AddAuthentication/AddAuthorization, removed `_ = configuration;`)
  - `tests/unit/Plexor.Modules.Sigil.Unit/Plexor.Modules.Sigil.Unit.csproj` (+FrameworkReference, +NSubstitute)

## Next
Phase 3.7 (`[RequirePermission]` attribute in
`Plexor.Shared.Authorization`) can land next, then Phase 4 (auth
controllers: `/auth/login`, `/auth/refresh`, `/auth/me`, user/role
endpoints). The bearer scheme is now in place — controllers will get
authenticated `HttpContext.User` claims from `VerifyResult.Success`.