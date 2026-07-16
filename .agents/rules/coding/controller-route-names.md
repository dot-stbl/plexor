---
description: c# controller route names — `[Http*(Name = ...)]` MUST be referenced via `file static class` constants, NEVER via `nameof(Method)` in `CreatedAtAction`
globs: ["**/*.cs"]
always: true
---

# Controller route names — file-static constants, not `nameof(Method)`

`CreatedAtAction`, `CreatedAtRoute`, and `RedirectToAction` resolve actions
by their **routing name** (the value of `[HttpGet(..., Name = "...")]` /
`[HttpPost(..., Name = "...")]` etc.), **NOT** by the C# method name.
Passing `nameof(GetAsync)` to `CreatedAtAction` fails at runtime with
`InvalidOperationException: Cannot resolve action 'GetAsync'` — the method
exists, but its routing name is `"<controller>-get"` (or whatever the
`Name =` attribute declared).

This is one of the easiest LLM mistakes to make: the LLM writes
`CreatedAtAction(nameof(GetAsync), ...)` because it looks like a
type-safe reference, but ASP.NET Core routing never sees C# method
names — it sees the literal string from `Name =`.

## Rule

**Every action attribute `[Http*/HttpGet/HttpPost/HttpPatch/HttpDelete]` that
declares `Name = "..."` MUST reference a `const string` from a
`file static class *RouteNames` declared at the top of the same file.
The same constant MUST be used in any `CreatedAtAction` /
`CreatedAtRoute` / `RedirectToAction` that targets that route.**

```csharp
// ✅ Correct — single source of truth, compiler verifies both call sites
file static class WorkloadRouteNames
{
    public const string Get = "workloads-get";
    public const string Create = "workloads-create";
    // ... etc
}

[HttpPost(Name = WorkloadRouteNames.Create)]
public async Task<ActionResult<WorkloadSummary>> CreateAsync(...)
{
    var summary = await createHandler.HandleAsync(...);
    return CreatedAtAction(
        WorkloadRouteNames.Get,                                // ← same const
        new { clusterId, workloadId = summary.Id },
        summary);
}

[HttpGet("{workloadId}", Name = WorkloadRouteNames.Get)]      // ← same const
public async Task<ActionResult<WorkloadSummary>> GetAsync(...)
{ ... }
```

```csharp
// ❌ Wrong — looks type-safe but routing resolves the literal string,
//              not the C# symbol. Throws 'Cannot resolve action' at runtime.
[HttpGet("{workloadId}", Name = "workloads-get")]
public async Task<ActionResult<WorkloadSummary>> GetAsync(...)

return CreatedAtAction(
    nameof(GetAsync),                                          // ← BUG: "GetAsync" ≠ "workloads-get"
    new { clusterId, workloadId = summary.Id },
    summary);
```

## Why

1. **Single source of truth** — one place to rename; one place to find the
   route name when wiring up tests / OpenAPI clients / cross-controller
   `RedirectToAction` calls.
2. **Compile-time verification** — renaming a const or breaking the
   `[HttpGet(Name = X)]` ↔ `CreatedAtAction(X, ...)` contract is a
   compile error, not a runtime `InvalidOperationException` triggered
   by the first POST to that endpoint.
3. **No reflection / no magic strings** — the literal `"workloads-get"`
   appears exactly once per file. The Roslyn `dotnet format` tool
   won't reformat it (no whitespace drift), and `[EndpointName]`
   decorators on other controllers can't accidentally shadow it.
4. **No `nameof()` confusion** — `nameof(GetAsync)` evaluates to
   `"GetAsync"` (the C# identifier) but ASP.NET Core's `ActionDescriptor`
   has no `ActionName` slot keyed on the C# identifier; it uses
   `ActionDescriptor.Name` which is set from `[HttpXxx(..., Name = "...")]`.
   When `Name =` is unset, `ActionDescriptor.Name` falls back to the
   method name, which is why `nameof` sometimes "works" — by accident,
   when the developer didn't set `Name =`. The moment you add
   `Name = "kebab-case-route"`, `nameof()` becomes wrong.

## Grouping

One `file static class` per **resource family** within a file. If a file
contains `IamRolesController` + `IamBindingsController` + `IamApiKeysController`,
declare three separate classes (`IamRolesRouteNames`,
`IamCredentialsRouteNames`, etc.) so the consts cluster with the
controller that uses them. Don't bundle unrelated routes into a single
"ControllerNames" bucket.

For one-controller-per-file (the project default), one
`*RouteNames` class per file is enough.

## When you can't add a `Name = ` attribute

Some actions legitimately have no routing name (e.g. utility endpoints
that are never the target of `CreatedAtAction` / `RedirectToAction`).
Don't add an empty `Name = ""` just to keep the pattern uniform — only
add the const when you need to reference the route from elsewhere.

If the action is the target of a `CreatedAtAction`/`RedirectToAction`
but you forgot to set `Name =`, the error message will tell you which
const name to add. Set `Name = "<resource>-<action>"` and reference
the new const from both call sites.

## Self-audit grep

```bash
# Any nameof() passed to a CreatedAtAction/CreatedAtRoute/RedirectToAction
# — should be ZERO matches in production code (test projects ignored).
rg -n "CreatedAtAction.*nameof|CreatedAtRoute.*nameof|RedirectToAction.*nameof" src/

# Any string-literal route name in [Http*(Name = "...")] — should be
# ZERO matches (always use the file-static const instead).
rg -nE '\[Http(Get|Post|Put|Patch|Delete)[^\]]*Name = "' src/

# Any string-literal action name in CreatedAtAction / CreatedAtRoute /
# RedirectToAction — should be ZERO matches (always use the const).
rg -nE 'CreatedAt(Action|Route)\(\s*"(Get|Create|Update|Delete|Post)' src/
```

A non-empty result from any of the three greps is a violation —
either rename the call site to use the const, or — if you genuinely
need a new route name — declare it in the `*RouteNames` class.

## Enforcement

No analyzer today. Convention + review + the self-audit grep above.
A future analyzer could:
- Walk the syntax tree and match every `CreatedAtAction(...)` call to
  the literal string at the `[Http*(..., Name = ...)]` attribute on the
  referenced method.
- Or: lint `nameof()` arguments and warn when the method's `[Http*]`
  attribute has an explicit `Name = "..."` that doesn't equal the C#
  identifier.

Until that's built, the file-static `*RouteNames` class is the
enforcement mechanism: a missing reference is a compile error.

## Known pre-existing violations (closed in 2026-07-16)

- `Plexor.Modules.Clusters.Api/Controllers/WorkloadsController.cs:64`
  — `CreatedAtAction(nameof(GetAsync), ...)` while `[HttpGet]`
  declared `Name = "workloads-get"`. Fixed in commit `db19917` via
  `WorkloadRouteNames` file-static class.
- `Plexor.Modules.Sigil.Api/Controllers/IamController.cs:64` —
  same pattern for `iam-users-get`. Fixed in `0213f48`.
- `Plexor.Modules.Sigil.Api/Controllers/IamControllers.cs:59` —
  same pattern for `iam-roles-get`. Fixed in `0213f48`. The same
  file had a dead-code `IamCredentialsControllerBase.CreatedAtKey`
  helper with `actionName: "GetAsync"` — deleted (no subclass used
  it). Fixed in `0213f48`.

## Related

- `coding/api-design.md` — controller skeleton, OpenAPI attributes,
  URL structure.
- `coding/anti-patterns.md` §"Wire-format types" — request/response
  DTO placement (sibling rule: separate file per type).
- `coding/constructors-and-fields.md` §"Constants — `file static class`" —
  the convention this rule reuses for the route-name constants.
