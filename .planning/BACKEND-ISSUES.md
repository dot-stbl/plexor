# Backend Issues — tracked technical debt

> Single source of truth for "what we know is broken / sub-optimal and why
> we have not fixed it yet". Each entry is a deliberate choice, not an
> oversight. Revisit each on the date in the `Revisit` column.

---

## NU1903 — Microsoft.OpenApi ≥ 2.0.0 pinned to 2.4.1 by upstream; CVE GHSA-v5pm-xwqc-g5wc

**Status**: blocked on upstream; mitigation in place.

**CVE**: [GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc)
(severity: HIGH). Affects `Microsoft.OpenApi` versions through 2.4.1 inclusive.

**Where it comes from**: `Microsoft.AspNetCore.OpenApi 10.0.9` ships
`<dependency id="Microsoft.OpenApi" version="2.0.0" exclude="Build,Analyzers" />`
on `net10.0`. NuGet resolves the floating 2.0.0–latest-2.x constraint to the
highest available 2.x release, currently `2.4.1`. Both 2.0.0 and 2.4.1 are
listed in the GHSA-v5pm-xwqc-g5wc advisory.

**Why we cannot bump to Microsoft.OpenApi 3.x**: the
`Microsoft.AspNetCore.OpenApi 10.0.9` source generator emits code targeting
the v2 wire-format API surface. Specifically, it generates lines like
`mediaType.Example = xmlCommentExample` against `IOpenApiMediaType.Example`,
which became a **read-only computed property** in `Microsoft.OpenApi 3.x`.
Forcing `Microsoft.OpenApi 3.x` via direct `<PackageReference>` produces
build errors in the generated code:

```
error CS0200: Cannot assign to 'IOpenApiMediaType.Example' — read-only.
```

`Microsoft.AspNetCore.OpenApi 11.0.0-preview.x` is the first version where
the source generator targets the v3 API surface, but it requires .NET 11;
we target `net10.0`. There is no `10.0.10+` release as of 2026-07-11.

**Mitigation**:
1. Explicit `<PackageReference Include="Microsoft.OpenApi" Version="2.4.1" />`
   on `Plexor.Host` and `Plexor.Shared.Filtering` — prevents the version
   float to 2.0.0 (which has a worse CVE shape than 2.4.1).
2. `Directory.Build.props` `<NoWarn>` carries `NU1903` with a comment
   pointing here. After Microsoft ships a `Microsoft.AspNetCore.OpenApi`
   patch (10.0.10+ or 10.1.x with v3 generator targeting), drop the NoWarn
   and migrate.
3. The `Plexor.Shared.Filtering.FilterableSchemaTransformer` uses the v2
   wire types (`JsonSchemaType`, `JsonNodeExtension`) so it builds against
   the current `Microsoft.OpenApi 2.4.1`. When the upstream pins lift,
   rewriting the transformer is a 1-file port (`JsonSchemaType` → break
   into separate fields, `IOpenApiMediaType.Example` is the only generator
   breakage).

**Severity assessment**: the CVE is in **the library's serialization
pipeline** (parsing non-conformant OpenAPI documents). The vector requires
adversary-supplied OpenAPI input. Plexor.Host does not currently **receive
** OpenAPI documents from untrusted sources — the only consumers are
Swashbuckle (`/scalar`) and the build-time codegen (`artifacts/openapi.json`).
Both are author-controlled. **Real-world impact at plexor.Host is bounded,
not zero**: a future feature that ingests third-party API specs (e.g.
import a foreign MCP server's OpenAPI) would expose the surface.

**Revisit**: re-evaluate on every `Microsoft.AspNetCore.OpenApi` release;
track via `.planning/STATE.md` infrastructure-decisions table.

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
