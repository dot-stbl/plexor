# Backend Issues — tracked technical debt

> Single source of truth for "what we know is broken / sub-optimal and why
> we have not fixed it yet". Each entry is a deliberate choice, not an
> oversight. Revisit each on the date in the `Revisit` column.

---

## NU1903 — Microsoft.OpenApi ≥ 2.0.0 pinned to 2.4.1 by upstream; CVE GHSA-v5pm-xwqc-g5wc

**Status**: accepted technical debt — no current exposure.

**CVE**: [GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc)
(severity: HIGH). Affects `Microsoft.OpenApi` versions through 2.4.1 inclusive.

**Where it comes from**: `Microsoft.AspNetCore.OpenApi 10.0.9` ships
`<dependency id="Microsoft.OpenApi" version="2.0.0" exclude="Build,Analyzers" />`
on `net10.0`. NuGet resolves the floating 2.0.0–latest-2.x constraint to
the highest available 2.x release, currently `2.4.1`.

**Why we cannot bump to Microsoft.OpenApi 3.x**: the
`Microsoft.AspNetCore.OpenApi 10.0.9` source generator emits code
targeting the v2 wire-format API surface (`IOpenApiMediaType.Example`
setter; became read-only computed in 3.x). Forcing 3.x via direct
`<PackageReference>` produces build errors in the generated code.
`Microsoft.AspNetCore.OpenApi 11.0.0-preview.x` is the first version
where the source generator targets the v3 API surface, but it
requires .NET 11; we target `net10.0`. There is no `10.0.10+`
release as of 2026-07-11.

**Exposure assessment in plexor v0.1**: **zero**.

The CVE is in the library's document parsing pipeline — the vector
requires an adversary-supplied OpenAPI document to parse. Plexor's
host both (a) builds its own document from author-controlled
controller signatures at build time, and (b) serves that document
back to whoever calls `/openapi/v1.json`. Plexor never **receives**
an OpenAPI document from the network. Author-controlled source
+ author-controlled consumer = no untrusted input path.

The only scenarios where this becomes exploitable are future-phase
features: MCP server import (`architecture/mcp.md`), third-party
API catalog integration, anything that feeds a foreign OpenAPI
document into the host. None scheduled for v0.1 / v0.2.

**Action**: do nothing. The `<NoWarn>NU1903</NoWarn>` entry in
`Directory.Build.props` is the correct disposition for v0.1. When
MCP-import or equivalent lands, decide based on (a) what 3.x wire
types Microsoft ships at that point and (b) whether our exposure
moved from "zero" to "real".

**Revisit**: when (a) Microsoft ships `Microsoft.AspNetCore.OpenApi
10.0.10+` targeting the v3 API surface **or** `11.0.0` GA — whichever
comes first; **or** when a phase ships that ingests external OpenAPI
documents (MCP import, etc.), whichever comes first.

---

## FilterableEntityRegistry — `x-plexor-type` extension is not auto-emitted

**Status**: design-time gap; manual workaround in place.

**Symptom**: `FilterableSchemaTransformer` reads the `x-plexor-type`
schema extension to look up the registered entity's field set. The extension
is **not** automatically attached to schemas by `Microsoft.AspNetCore.OpenApi
10.0.9`'s default schema-generation pipeline. `PlexorTypeSchemaTransformer`
is currently a no-op stub because the
`OpenApiSchemaTransformerContext` does not expose the CLR type the schema
was generated from (a known gap in the v10 source generator).

**Consequence**: every filterable entity needs a manual `x-plexor-type`
extension on its schema, set via a controller-level schema transformer
that knows the action's response type. The wiring is verbose and easy
to forget.

**Mitigation today**: the contract layer (`Plexor.Shared.Contracts`) sets
the extension on the response DTOs manually via a thin `TypeOpenApiExtender`
helper. Until the OpenAPI generator gap closes, every `ProducesResponseType<T>`
on a list endpoint must go through the extender or filterable entities will
be invisible.

**Cleaner path forward**:
1. Drop `Microsoft.AspNetCore.OpenApi` and switch to **Swashbuckle +
   NSwag** OpenAPI generation. Swashbuckle's `ISchemaFilter` interface
   exposes the CLR type from the schema-generation context, fixing the
   root cause.
2. Wait for `Microsoft.AspNetCore.OpenApi` 10.0.10+ to expose CLR-type
   context (track on every release).
3. Or: scan the document post-generation in a custom
   `OpenApiDocumentTransformer` and match schemas by name to entities
   imported into the host via `AddFilterableEntity<T>`.

**Revisit**: when Microsoft ships a v10 source-generator update, or
when we migrate to Swashbuckle for OpenAPI generation.

---

## MigratorOwnedDeps — `Plexor.Migrator` carries EF Core but does not register any DbContext

**Status**: known, by design, will be cleaned up when first DbContext lands.

**Detail**: `Plexor.Migrator.csproj` references `Microsoft.EntityFrameworkCore.Design`
so the `dotnet ef migrations add` CLI works against any module's DbContext
without a second copy of the packages. The Migrator itself does not yet
register DbContexts in DI (Phase 1 of the persistence migration in
`architecture/persistence.md` not yet executed). The EF Core packages will
become useful when the first migration runs (Phase 1, planned).

**Action**: none until Phase 1 kicks off. When the first module DbContext is
introduced, this entry becomes the checklist for the migrator pipeline.
