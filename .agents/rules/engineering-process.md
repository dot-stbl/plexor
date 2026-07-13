# Engineering rules — process

> Engineering-zone work. These rules are non-negotiable. Two
> processes the agent violated in the NodeAgent bootstrap incident
> (Jul 2026) that this file was written to prevent from repeating.

## 1. Discuss architecture before any non-trivial coding

For any change that introduces a **new module, project, or
non-trivial abstraction** (more than a few files, or new public
APIs, or new cross-cutting concern), the agent must present an
architecture sketch and get explicit approval before writing code.

The NodeAgent incident: 17 new files (~1200 lines) were produced
in a single commit. None of the file boundaries, interface shapes,
DI registration order, or build target were discussed first. The
caller had to read 17 unfamiliar files to even form an opinion, and
the result required a full revert.

**What "discuss" means:**
- One message describing: which projects to add, which interfaces
  vs concrete classes, which files vs which folder, how it slots
  into the existing solution (`plexor.slnx`), and which build
  commands verify it.
- The caller approves, modifies, or rejects. Only then do you
  start writing.
- The discussion must be specific enough to commit to: "file X will
  be in folder Y with public interface IZ" — not "let me design
  as I go."

**What does NOT need a discussion:**
- A bug fix in one file
- A one-file change that follows an existing pattern
- A trivial refactor (rename, extract small helper)

When in doubt: ask first. The cost of a 5-message discussion is
minutes; the cost of a 17-file revert is hours.

## 2. Build after every file in a new area

The compiler is the cheapest feedback signal. The NodeAgent
incident produced 23+ compile errors that took 30+ minutes of
mechanical edits to chase down. Each edit that broke the build was
committed and built upon, because there was no "is the build still
green?" check between edits.

**Rule:** when introducing a new project, new interface, or new
public API:

1. **After the first file of the new area is created**, run the
   build. The build must pass before the second file is opened.
2. **After every subsequent file**, run the build. A file that
   fails to compile is not "done."
3. **If the project doesn't exist yet**, run `dotnet new` for the
   template, then `dotnet build` on the empty project, before
   writing the first line of business logic. This catches csproj
   config errors before they become "mysterious 30+ errors."

**Concretely, for .NET:**
- New project: `dotnet new classlib` + `dotnet build` (empty) + then
  fill in
- New file: save, then `dotnet build` before opening the next file
- For an existing project: `dotnet build` after every change in a
  new namespace or new interface (those are the highest-risk edits)

## 3. Small interfaces, public modifiers, IDE-friendly syntax

The repo enforces `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
in `Directory.Build.props`. Analyzers catch:

- `IDE0040` — interface members need **explicit `public` modifier** in
  C# 12+ file-scoped namespaces. Records' *positional parameters*
  do NOT — the modifier applies to the record itself, not its
  components. Mixing modifiers inside one record is a syntax error.
  Rule: `public sealed record X(...)` (modifier on record) and
  `void Method(...)` (no modifier in interface) are the right shapes.
  Plus strict XML doc convention (CS1591 is an error in this repo):
  - Every public type and every public member MUST have at least
    `/// <summary>…</summary>`. The summary is the doc; not a `//`
    comment.
  - `/// <param name="X">…</param>` is REQUIRED only when the
    parameter's purpose is NOT obvious from the name+type. Examples:
    `string Hostname` (obvious) does NOT need a `<param>`; but
    `DateTimeOffset SentAt` (why the timestamp, what the control
    plane uses it for) DOES. Over-documenting is noise; under-
    documenting breaks CS1591.
  - `/// <inheritdoc cref="X.Y" />` is the canonical way to inherit
    the base doc on a property override or constructor override.
    Inheritdoc does NOT work on property overrides in C# 12 (the
    compiler reports CS1591 for the override) — for those, write a
    one-line `<summary>` directly. Use `<inheritdoc />` only on
    method, constructor, and interface implementation overrides where
    the compiler accepts it.
  - XML comments are NOT source code — they live in the docs stream.
    Escape rules: only `<`, `>`, `&` are special. `cref="…"` uses
    raw C# identifiers; the C# compiler resolves them at build.
    Never put `&` or `<` in text content without `&amp;` / `&lt;`.
- `IDE0011` — add braces to single-line `if`/`foreach`. Never omit.
- `VSTHRD103` — sync I/O blocks async. Use `await X.ReadAsync(...)`
  on every `StreamReader` / `Process` / `HttpClient` / `JsonSerializer`.
- `VSTHRD200` — top-level statements that return Task end with
  `MainAsync` in C# 12. Use `#pragma warning disable VSTHRD200` on
  the Program.cs file if the implicit main is the source of the
  warning; do NOT add `Async` to a non-Task-returning method to
  silence it.
- `IDE0022` — use expression-bodied members where the body is a
  single expression. Multi-statement bodies must use a block.

**Rule:** Before committing, run `dotnet build` and `dotnet format
--verify-no-changes` on the changed files. If format wants to touch
something, let it.

## 4. Local mock data, single in-memory store for v0.1

The first iteration of a new module is a **spike**, not a feature. The
infrastructure for the production version (Postgres, NATS,
distributed locks) is built in a later iteration.

For the **first commit of a new module:**
- In-memory dict or `ConcurrentDictionary` for the state store
- Mock / hand-curated data
- Single-node assumption
- No auth, no mTLS, no multi-tenancy

Add a comment in the file header: "v0.1: in-memory store, no
persistence. Production version is a later iteration." That way
reviewers don't expect a real DB on the first PR.

The NodeAgent incident is the second time this lesson had to be
relearned. The original plexor docs say: "DB-of-record = PostgreSQL.
For v0.1, in-memory state. Production version: persist."
This rule formalizes it.

## 5. Run the full build before claiming "done"

When the work is finished (all files written, all rules followed):

```
cd /c/Users/bradw/source/stbl/plexor
dotnet build plexor.slnx
```

Zero warnings. Zero errors. The user should be able to clone, build,
and run without reading the diff.

The NodeAgent incident left the working tree with a partial
NodeApi commit + 17 untracked files + 23+ compile errors. The
"verification" was just `git status` saying "17 files modified" —
not a build. The user saw the build errors only when they tried to
run the project.

**Rule:** "I'm done" means "the build is green on the clean working
tree." Not "I wrote all the files I meant to write."

## 6. When a rule applies, do not improvise

A rule is a contract. Following it 80% of the time and improvising
the other 20% produces bugs that look like "the rule didn't apply
here." The rule DID apply. Read it again.

The NodeAgent incident: the user said "build after every file" and
the agent built once at the end. "Build after every file" means
"after every file." The exception "but this is just one new project
and I want to get them all in first" is not an exception — it's
a workaround that hides errors.

If a rule can't be followed, that's a rule-design problem — ask the
user to update the rule, don't silently skip it.

---

## When in doubt

- "Should I discuss this first?" — yes, if the work spans more
  than ~2 files or introduces a new public API.
- "Should I run the build?" — yes, after every file in a new area.
  Not negotiable.
- "Should I run `dotnet format`?" — yes, before committing.
- "Should I commit a partial state?" — no, not without an explicit
  agreement. A `WIP` commit that doesn't compile is a hazard.
