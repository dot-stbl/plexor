# Design: phase-4-6-auth-providers

Technical decisions for Phase 4.6.1 (configuration layer). The
bearer-handler + flow-endpoint decisions land in their own
sections when those changes get written.

## Where the entity lives

`OrgAuthProviderConfig` lives in `Plexor.Modules.Realm` (the
Realm module owns tenants). The schema is `realm` (architecture
theme), the table is `org_auth_provider_configs`, the C#
class name follows the domain concept ("org auth provider
config") rather than the schema name (per the root
`AGENTS.md` rule on naming).

The controller lives in `Plexor.Host/Controllers/` rather than
in a new `Plexor.Modules.Realm.Api` project. Rationale:
Plexor.Modules.Realm has no Api project today (no Api-layer
concerns shipped yet); creating one for a 3-endpoint surface
is a refactor the user explicitly deferred. v1 keeps the
controller in the host assembly, auto-discovered by
`AddControllers()`. A future move to `Plexor.Modules.Realm.Api`
is a one-class relocation.

## Encryption-at-rest via `IDataProtector`

The OIDC client secret is the only piece of state that crosses
the trust boundary — a Sigil password hash is bcrypt'd by
`IPasswordHasher`, an API key is SHA256'd, but the OIDC
client secret lands in our table verbatim if we don't encrypt
it. `Microsoft.AspNetCore.DataProtection` is the platform
answer:

- `AddDataProtection()` is registered once in
  `Plexor.Host/Program.cs` with `ApplicationDiscriminator =
  "plexor-host"` so a future sibling process (e.g. the
  migrator CLI) can mint a purpose-bound protector with the
  same keyring.
- `PersistKeysToFileSystem(<data-root>/dataprotection-keys/)`
  — the OS-conventional Plexor data root is
  cross-platform (LocalAppData on Windows,
  `XDG_DATA_HOME/plexor` on Linux, `~/Library/Application
  Support/plexor` on macOS). Multi-host requires the
  directory to be a shared volume; self-host (single pod) is
  the v0.1 shape.
- The controller creates a purpose-bound protector at the
  call site: `dataProtectionProvider.CreateProtector("OrgAuthProviderConfig.OidcClientSecret")`.
  The purpose string is shared with the controller via the
  `OrgAuthProviderSecretProtector.Purpose` constant in
  `OrgAuthProvidersController.cs`. A different-purpose
  protector elsewhere cannot decrypt the same ciphertext —
  defense in depth against a misconfigured DI registration.

The decrypted secret is held in memory only for the duration
of the HTTP call (in `OrgAuthProvidersController.TestAsync`).
It is never logged, never returned in the response, never
persisted in plaintext. `TestAsync` decrypts locally for an
outbound OIDC discovery fetch; the response carries only the
discovery URL + scopes + connection-error string.

## Tenant isolation: 404, never 403

A user authenticated in Org X SHALL NOT view or mutate Org Y's
auth-provider config. The shape is `404 Not Found` — never
`403 Forbidden`. A `403` would leak the existence of the
target org; `404` makes the caller unable to distinguish "not
found in your tenant" from "not found at all". The check is
the same `orgId != currentUser.TenantId` guard the Quotas
controller uses (`quota-scope checks` in
`openspec/specs/quotas/spec.md`).

## In-place update via `ExecuteUpdateAsync`

The entity uses `init`-only properties (project convention —
mirrors `User`, `Organization`, `Folder`, `Role`, etc.).
Mutating an `init`-only property after construction is a
compile error. Two options:

1. Switch the entity to `set;` properties — breaks the
   convention across every entity in the project.
2. Use EF's `ExecuteUpdateAsync` — emits a SQL UPDATE without
   loading the entity into memory, sidestepping the
   init-only property issue entirely.

Chose option 2. The PUT controller path uses
`ExecuteUpdateAsync(setters => ...)` for both the
Sigil-reset and the OIDC-upsert branches. The controller
re-reads the row (AsNoTracking) for the response so the
client sees the persisted state. Same pattern as the
SigIL `UpdateUserCommandHandler` (4.5.b).

## OIDC discovery fetch — HTTP client with no auth

The `POST /test` endpoint fetches
`{authority}/.well-known/openid-configuration` via a
named `IHttpClientFactory` client
(`"Plexor-OidcDiscovery"`):

- `Timeout = 10 seconds` (long enough for cold-keycloak;
  short enough that a hung endpoint doesn't block the
  worker thread).
- `User-Agent = Plexor-Host/0.1` (operator-visible in the
  authority's access log; standard for tooling).

A separate client keeps the default request policy (no
auth, no retries) out of any future client that wants
Polly resilience. Future: when the bearer handler routes
to OIDC, it uses a different named client (`Plexor-OidcResource`)
with retry/circuit-breaker; the discovery client stays
single-shot.

## File-static helpers + `internal sealed` controller

The controller is `public sealed` (auto-discovery needs a
public type). The helpers file is `internal static` —
visible inside `Plexor.Host` but not part of the public
surface. The EF seeder is `internal sealed` and the test
project gets it via `InternalsVisibleTo("Plexor.Modules.Realm.Unit")`.

The helpers file pattern (file-static, extracted from
the controller) matches the Quotas controller — every
mapping + ProblemDetails construction + JSON read lives
in a flat static class. The controller body is the thin
orchestration: validate → fetch → mutate via ExecuteUpdate
→ re-read → map → return.

## Why no `IFilterableEntity` on `OrgAuthProviderConfig`

The entity is one-row-per-org. There's no list endpoint
(`GET /api/v1/iam/orgs/{orgId}/auth-provider` returns exactly
one row). Adding `IFilterableEntity` would force
`Plexor.Modules.Realm.Domain` to take a project reference on
`Plexor.Shared.Filtering.Registry` for no benefit. The
Sigil module has the same shape (`User`, `Role` etc.) and
also skips `IFilterableEntity` on entities without list
endpoints.

## InMemory test provider + `text[]`

The `text[]` column type with `IReadOnlyList<string>`
conversion is incompatible with the EF InMemory test
provider — InMemory can't compose a converter chain that
ends in `string[]`. The fix used: convert to/from
`IEnumerable<string>` instead of `string[]`. The
InMemory provider has its own `IEnumerable<string> →
string` serializer (JSON); the composition now
succeeds because my converter's output is `IEnumerable<string>`
which is the InMemory provider's expected input. The
Npgsql provider sees the `text[]` column type and stores
the array natively. Both providers work, no
test-only schema changes required.

## Open questions deferred (4.6.2 / 4.6.3)

- **JWKS cache TTL** — first request fetches, subsequent
  refresh on `kid` miss. Cache storage lives in
  `Plexor.Modules.Sigil.Infrastructure.Auth` (the OIDC
  provider implementation).
- **Discovery-document cache TTL** — same shape.
  The `POST /test` endpoint is intentionally NOT cached —
  it's an operator-driven probe, not a hot path.
- **Secret rotation without downtime** — every PUT
  rewrites the ciphertext. v0.1 callers are expected to
  accept a brief re-issuance window. Phase 5+ adds an
  overlap window (both secrets valid for N hours) when
  OIDC discovery advertises multiple JWKS kids.
- **Per-org audit emission** — `OrgAuthProviderChanged`
  events for the existing `atlas` consumer. The current
  controller logs `LogInformation` with structured fields;
  the `IOrgAuthAuditEmitter` integration lands in 4.6.2
  alongside the audit-emission seam.
