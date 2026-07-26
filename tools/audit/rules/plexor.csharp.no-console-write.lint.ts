/**
 * plexor.no-console-write — project rule for Plexor.
 * Mirrors .agents/rules/coding/no-console-write.md.
 *
 * Production code in C# must log through ILogger<T>, not Console.
 * Justification: structured levels, sinks, correlation IDs, categories,
 * format control, and testable assertions. Console.WriteLine is opaque.
 *
 * Forbidden (production code):
 *   Console.WriteLine(...)
 *   Console.Write(...)
 *   Console.Error.WriteLine(...)
 *   Console.Out.WriteLine(...)
 *   System.Console.WriteLine(...)
 *   System.Diagnostics.Debug.WriteLine(...)
 *
 * Legitimate (legitimate exceptions, must verify in review):
 *   AnsiConsole.WriteLine(...) -- Spectre.Console CLI output;
 *     acceptable in installer / CLI tooling before ILogger is reachable.
 *   Console.WriteLine inside Main(args) of throwaway CLI scripts
 *     in Plexor.Installer.Cli before the Spectre host spins up.
 *   System.Diagnostics.Trace.WriteLine inside #if DEBUG blocks.
 *
 * AST approach via RE2: pattern matches Console.Write|Console.Error.Write|
 * Console.Out.Write on a non-Spectre caller. excludeWhen skips the
 * AnsiConsole.Spectre output shape and the Plexor.Installer.Cli throwaway-
 * script shape (verified exception).
 */
export default {
  id: 'plexor.no-console-write',
  severity: 'warning',
  pattern:
    '(?:^|[^\\w.])(?:System\\.)?(?:Diagnostics\\.Debug\\.)?(?:Console)\\.(?:Write(Line)?|Error\\.Write(Line)?|Out\\.Write(Line)?)\\s*\\(',
  excludeWhen:
    '(?:^|[^\\w.])(?:AnsiConsole)\\.' ,
  globs: ['**/*.cs'],
  excludePaths: [
    '**/bin/**',
    '**/obj/**',
    '**/Migrations/**',
    '**/*.Designer.cs',
    '**/*.g.cs',
    '**/*.AssemblyAttributes.cs',
    '**/Generated/**',
    '**/installer/Plexor.Installer.Cli/**',
  ],
  message:
    'Console.WriteLine in production code -- log through ILogger<T> for structured levels / sinks / correlation IDs. See .agents/rules/coding/no-console-write.md.',
  source: 'coding/no-console-write.md',
  rationale:
    'ILogger<T> writes to every registered sink (console, OTel, file, seq, loki) with per-category routing, level filtering, traceparent injection, and format control (PlexorConsoleFormatter). Console.WriteLine goes to one process stdout/stderr; tests cannot intercept it; log aggregators cannot parse it; per-category admin-vs-runtime split cannot route it. The Plexor.Installer.Cli and Spectre-Console output paths are the documented exceptions.',
};
