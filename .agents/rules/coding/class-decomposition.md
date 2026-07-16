---
description: classes > 300 lines or with private helpers must be decomposed into focused classes or extension methods
globs: ["**/*.cs"]
---

# Class decomposition — when to split, where to put what

Two thresholds, both hard. A class that crosses either one **must**
be decomposed before merging. The shape of the decomposition is
case-by-case — this rule defines the *trigger*, not the fix.

## Threshold 1: file length > 300 lines

Any `.cs` file (production code) longer than **300 lines** is a
refactor signal. Long files:

- Make the file hard to read end-to-end (the eye loses the thread
  around 200 lines).
- Hide couplings — a reader has to scroll to discover what the
  class really depends on.
- Resist testing — a 500-line class typically has 3+ collaborators
  that want to be tested in isolation but can't because they're
  baked into the same type.

**Refactor**: split along the class's actual responsibilities. A
controller that has a command, a query, and a mapping helper is
three classes; a service that has a builder, a verifier, and a
processor is three classes. Don't split along arbitrary line
counts — split along what each piece *does*.

**Refactor targets** (in order of preference):

1. **Inject the collaborator** — if a 30-line method only touches
   one other class, the method probably belongs on that other
   class, not here. Move the method to the collaborator, call it.
2. **Extract to a service** — if the 30 lines have their own
   dependencies (a repo, a logger, a clock), make a new class with
   those dependencies; inject it.
3. **Extract to a static helper** — if the 30 lines are pure
   (no `this.` state, no I/O, no side effects), put them in a
   `file static class <Something>Helpers` in the same folder.
4. **Extract to an extension method** — if the 30 lines add new
   behaviour to a type they don't own, put them in
   `*Extensions.cs` as a `file static class`.

The point isn't "shorter file" — it's "each file does one thing".

## Threshold 2: > 2 private methods (extension of §9)

The §9 anti-pattern rule already says production classes shouldn't
have private methods. **The threshold is "more than two"** —
two is the boundary because (a) a constructor + a single
`Dispose` is normal, and (b) two helpers may indicate the class
wants a small extraction (three is unambiguous). When you have
**three or more private methods**, the class wants to be split:

- **Pure helpers** → `file static class` in same folder.
- **Domain methods** (operate on `this.<state>`) → extract a
  collaborator; inject it.
- **Cross-cutting helpers** (logging, formatting, validation
  that doesn't depend on instance state) → extension method on
  the type the helper operates on, in `*Extensions.cs`.

**Testability bonus**: decomposed collaborators are easier to
test in isolation than a method buried inside a 500-line class.
A 30-line helper extracted to a `file static` is trivial to
unit-test (no mocks, no DI).

## Anti-pattern: the "helpers" region

A common escape hatch is the "Helpers" region:

```csharp
public sealed class MyService
{
    public Task<...> DoWork(...) { ... }

    #region Helpers
    private static string FormatX(...) { ... }
    private static string ParseY(...) { ... }
    private static bool ValidateZ(...) { ... }
    #endregion
}
```

Don't do this. **#region** directives are banned
(`code-shape.md` §formatting). More importantly, "private static
helpers" at the bottom of a class is a smell — the class wants to
be a service + a helpers file. Split:

```csharp
// MyService.cs
public sealed class MyService(...) { ... }

// MyServiceHelpers.cs
file static class MyServiceHelpers
{
    public static string FormatX(...) { ... }
    public static string ParseY(...) { ... }
    public static bool ValidateZ(...) { ... }
}
```

The helpers become a `file static class` (not `public` — internal
to the assembly, but visible to MyService without DI). Tests
cover them without dragging in `MyService`'s collaborators.

## When extending an existing type

Extension methods on a type you don't own (string, int,
`IConfiguration`, etc.) belong in `<TypeName>Extensions.cs`:

```
src/services/Reporting/.../ReportRequestExtensions.cs
file static class ReportRequestExtensions
{
    public static ReportRequest WithDefaults(this ReportRequest request) { ... }
    public static bool IsValid(this ReportRequest request) { ... }
}
```

The `*Extensions.cs` filename pattern + `file static class`
together signal "these are extension methods, internal to the
assembly" — no DI registration, no public API surface.

## When not to extract

- **Constructors** — primary ctor + dispose pattern: that's 1-2
  methods max, not "private helpers".
- **Override methods** (`override Configure`, `override
  ConfigureBanners`) — they belong to the base contract, not
  the class's own logic. Count them separately.
- **Trivial one-liners** — `private string Name() => _name;`
  isn't a helper, it's a property in disguise.
- **Test stubs in test projects** — test code is allowed to grow
  large; rules §9 explicitly exempts tests.

## Self-audit before commit

```bash
# Lines per file in changed/added files
git diff --name-only --diff-filter=AM | xargs wc -l | sort -rn | head

# Private methods in changed/added classes
git diff --name-only --diff-filter=AM | xargs grep -c "private " 2>/dev/null \
    | grep -v ":0$" | sort -t: -k2 -rn | head
```

If any file is over 300 lines OR any class has > 2 private methods,
split before committing.

## Related

- `code-shape.md` §9 — anti-pattern "private method in production class" (this rule is the positive formulation)
- `code-shape.md` §formatting — no `#region` directives
- `project-layers.md` — where extracted classes go (which project, which folder)
- `class-layout-and-tooling.md` — XML docs, model placement, tooling
