---
description: "Production code must not use Console.WriteLine/Write. Structured logging goes through ILogger<T> so log aggregators, level filtering, and per-category sinks all work. Console.WriteLine is an opaque side channel that breaks observability."
globs: ["**/*.cs"]
priority: high
---

# No `Console.WriteLine` in production code

> **Production code logs through `ILogger<T>`, not `Console.WriteLine`.**
> Structured logs are aggregated, level-filtered, sinked per category,
> traceable through correlation IDs, and grep-able in production. Console
> writes are an opaque side channel that breaks all of that.

## The rule

In code that's reachable from `WebApplication`/`Host`/`IHostedService`,
use `ILogger<T>` injected via the primary constructor:

```csharp
// ✓ Correct — structured, level-filtered, sinked per category
public sealed class AuditQueryService(ILogger<AuditQueryService> logger)
{
    public async Task<IReadOnlyList<AuditEntry>> ListAsync(...)
    {
        logger.LogInformation("Listing audit entries between {From} and {To}", from, to);
        // ...
    }
}
```

The following are forbidden in production code:

```csharp
// ✗ Wrong — opaque, unleveled, unsinked
Console.WriteLine("starting up");
Console.Write($"hello {user}");
Console.Error.WriteLine("something went wrong");
Console.Out.WriteLine("done");
System.Console.WriteLine("...");   // qualified form, same ban
System.Diagnostics.Debug.WriteLine("...");  // debug-only, also unleveled
```

## Why

- **No level filtering.** An `Error` log and a `Debug` log are
  indistinguishable. `Ilogger.LogCritical` vs `LogInformation` lets
  ops tune sinks per severity.
- **No sinks.** Console output goes to the process's stdout/stderr only.
  `ILogger` writes to every registered sink (console, OpenTelemetry,
  file, seq, loki, ...). Adding a new sink is a config change; adding
  `Console.WriteLine` calls is a code change.
- **No correlation IDs.** `ILogger` scope providers inject
  `traceparent` / correlation ids into every log. Console writes can't.
- **No categories.** `ILogger<T>` filters per category (the `T` in the
  generic). `Console.WriteLine` writes to the global stream.
- **No format control.** Logs have a single output format (the
  `PlexorConsoleFormatter`). Console writes vary by author.
- **Tests can't suppress.** `ILogger` test doubles let tests assert
  what was logged. `Console.WriteLine` cannot be intercepted.

## Self-audit grep

```bash
rg -n "Console\.Write(Line)?\b|Console\.Out\.Write|Console\.Error\.Write|System\.Diagnostics\.Debug\.Write" src/ --type cs
# → Each hit: switch to logger.LogXxx.
```

The grep is by design not a build-time error — there are legitimate
non-production uses (see below) that we'd false-positive on. Code
review + the grep catch it.

## Legitimate exceptions

`Console.WriteLine` is OK in **code paths with no logging
infrastructure**:

- **Top-level entry points of throwaway scripts** (e.g. `Program.cs`
  in `plx` CLI before the `Spectre.Console` host spins up; or a
  one-off EF Core migration debug print inside a `dotnet ef` custom
  tool).
- **`Main(string[] args)` in console-style apps** that intentionally
  write to stdout (the very small startup window between `args`
  parsing and `IHost.Build()` — once `ILogger` is reachable, switch).
- **`System.Diagnostics.Trace.WriteLine` in a debugger-only
  conditional compilation block** (`#if DEBUG`).

These are exceptions to verify in review, not excuses for production
code. The rule is "default to `ILogger`; `Console.WriteLine` only when
there's no other way."

## Related

- `coding/logging.md` — the structured-logging rules (parameterized
  templates, level selection, no PII).
- `architecture/identity.md` — uses `logger.LogXxx` for every audit
  event in the Identity flow.
- `Plexor.Shared.Telemetry/PlexorConsoleFormatter` — the Plexor
  console formatter (custom level coloring + format); output of
  `ILogger`, not `Console.WriteLine`.
