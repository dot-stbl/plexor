# Plan: identity/phase-3-6-bearer-handler

## Goal

Wire the JWT signing service (Phase 3.3-3.4) into ASP.NET Core
authentication so that `[Authorize]` works out-of-the-box. A
`BearerAuthenticationHandler` reads the `Authorization: Bearer <jwt>`
header, calls `IJwtSigningService.VerifyAsync`, and on `Success` builds
a `ClaimsPrincipal` from the JWT claims; on `Invalid`/`Malformed` it
returns `AuthenticateResult.Fail` and the middleware produces a 401.

## Steps

1. **Add `BearerOptions` + `BearerAuthenticationHandler`** in
   `src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Auth/Bearer/`:
   - `BearerOptions : AuthenticationSchemeOptions` — empty marker for
     future tunables (allowed clock skew, required audiences). For now
     it just sets `AuthenticationSchemeOptions.DefaultScheme`.
   - `BearerAuthenticationHandler(IJwtSigningService keys, ILogger<…> logger)
     : AuthenticationHandler<BearerOptions>` — `HandleAuthenticateAsync`
     reads the `Authorization` header, splits on space, passes the token
     to `IJwtSigningService.VerifyAsync(ct)`, and pattern-matches on the
     sealed hierarchy:
     - `Success(Principal)` → `AuthenticateResult.Success(ticket, properties)`
     - `Invalid(Reason)` / `Malformed(Reason)` → `AuthenticateResult.Fail(reason)`
   - `HandleChallengeAsync` produces a `Bearer` challenge with the
     realm from `BearerOptions.Realm` (default `"plexor"`).
   - `HandleForbiddenAsync` is a no-op (default body is sufficient).

2. **Register the scheme** in
   `src/modules/Plexor.Modules.Sigil/Plexor.Modules.Sigil.Infrastructure/Installers/SigilInfrastructureInstaller.cs`:
   ```csharp
   builder.Services
       .AddAuthentication(BearerDefaults.AuthenticationScheme)
       .AddScheme<BearerOptions, BearerAuthenticationHandler>(
           BearerDefaults.AuthenticationScheme, _ => { });
   builder.Services.AddAuthorization();
   ```
   Constants pulled from `Microsoft.AspNetCore.Authentication.Bearer`
   (`AuthenticationSchemeConstants.Bearer` may differ between
   `Microsoft.AspNetCore.Authentication` and `System.Net.Http` — use the
   literal `"Bearer"` for now and wrap in a const in
   `Plexor.Modules.Sigil.Application.Auth` if it stabilizes).

3. **Add `<Sdk name="Microsoft.NET.Sdk.Web" />` … wait**. The
   `Sigil.Infrastructure` project is currently `Microsoft.NET.Sdk`. The
   `AddAuthentication`/`AddAuthorization` extension methods live in
   `Microsoft.AspNetCore.Authentication` / `Microsoft.AspNetCore.Authorization`
   which require the shared framework. Two options:
   - Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
     to `Plexor.Modules.Sigil.Infrastructure.csproj` (cleanest, matches
     what `Plexor.Host` already does).
   - Or call `AddAuthentication` from `Plexor.Host` Program.cs instead.
   Pick option (a) — keeps the auth wiring co-located with the JWT
   service it depends on.

4. **Unit tests** in
   `tests/unit/Plexor.Modules.Sigil.Unit/Auth/BearerAuthenticationHandlerShould.cs`:
   - `HandleAuthenticateAsync` returns `Fail` when `Authorization`
     header is missing.
   - `HandleAuthenticateAsync` returns `Fail` when scheme is not
     `Bearer`.
   - `HandleAuthenticateAsync` returns `Fail` when `IJwtSigningService.VerifyAsync`
     returns `Malformed`.
   - `HandleAuthenticateAsync` returns `Success` with a
     `ClaimsPrincipal` whose identity name matches the JWT `sub` when
     `VerifyAsync` returns `Success` (use a real
     `ECDsa.Create(ECCurve.NamedCurves.nistP256)` and roundtrip the
     token to make this end-to-end).
   - `HandleChallengeAsync` returns a 401 with `WWW-Authenticate: Bearer realm="plexor"`.

5. **Self-gate before commit**:
   - `dotnet build plexor.slnx -c Debug` — must succeed with the
     `Bearer` handler wired in.
   - `dotnet test tests/unit/Plexor.Modules.Sigil.Unit` — the four
     new facts pass.
   - `bash tools/review.sh` — 8/8 green.

## Acceptance

- `[Authorize]` attribute on a controller action triggers a 401
  response with `WWW-Authenticate: Bearer realm="plexor"` header
  when no token is supplied.
- A valid JWT signed by the active signing key produces an
  authenticated context where `HttpContext.User.FindFirst("sub")`
  returns the JWT subject claim.
- An invalid signature or expired token produces a 401 with reason
  in the response header (only in Development; suppressed in
  Production).
- Unit tests cover the four scenarios above; all pass.
- `dotnet build` stays at 0 warnings / 0 errors; the `Bearer`
  handler is part of the standard auth pipeline so
  `[Authorize(Policy = "...")]` works in later phases without extra
  wiring.
