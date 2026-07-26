/**
 * plexor.controller-name-is-const — project rule for Plexor.
 * Mirrors .agents/rules/coding/controller-route-names.md.
 *
 * Every [HttpGet|HttpPost|HttpPatch|HttpPut|HttpDelete] attribute that
 * declares Name = "..." MUST reference a const string from a file static
 * class *RouteNames declared at the top of the same file. The same
 * constant is the only valid argument to CreatedAtAction / CreatedAtRoute
 * / RedirectToAction.
 *
 * Forbidden (production code):
 *   [HttpPost("login", Name = "auth-login")]
 *   [HttpGet(Name = "clusters-list")]
 *
 * Correct:
 *   file static class AuthRouteNames {
 *     public const string Login = "auth-login";
 *   }
 *   [HttpPost("login", Name = AuthRouteNames.Login)]
 *
 * Why: a missing reference is a compile error (not a runtime
 * InvalidOperationException), renaming is one constant, no reflection
 * needed in tests, no magic strings scattered across [Http*] attributes.
 *
 * Strategy (RE2): per-line. Match a line that opens with [Http* and
 * contains Name = "literal" later on the same line. The single-line
 * scope (RE2) means multi-line attribute arguments are still matched
 * when collapsed onto one source line; multi-line spanning Name = "..."
 * across newlines is out of scope (rare in plexor; review catches).
 *
 * Globs limit to controller files only -- the Name = "..." literal
 * pattern is well-known to controllers via the [Route(Name=...)]
 * attribute, never to entity / model files.
 *
 * Allows:
 *   [HttpGet(Name = AuthRouteNames.Get)]            -- already const
 *   [HttpPost(Name = RouteNames.Create)]            -- any *RouteNames const
 *   [HttpGet]                                       -- no Name at all
 *   [HttpGet("path")]                               -- positional path only
 */
export default {
  id: 'plexor.controller-name-is-const',
  severity: 'warning',
  pattern:
    '\\[Http(?:Get|Post|Put|Patch|Delete)\\b[^\\]]*?Name\\s*=\\s*"([A-Za-z][\\w-]*)"',
  globs: ['**/*Controller.cs'],
  excludePaths: [
    '**/bin/**',
    '**/obj/**',
    '**/Migrations/**',
    '**/*.Designer.cs',
    '**/*.g.cs',
    '**/*.AssemblyAttributes.cs',
    '**/Generated/**',
  ],
  message:
    '[Http*(Name = "literal")] -- use a const from a file static class RouteNames instead. See .agents/rules/coding/controller-route-names.md.',
  source: 'coding/controller-route-names.md',
  rationale:
    'CreatedAtAction / CreatedAtRoute / RedirectToAction resolve by the literal route-name string, not by a C# method name. Passing nameof(GetAsync) fails with InvalidOperationException. The fix is one source of truth (a const string in a file static class *RouteNames) referenced by both [Http*(Name = RouteNames.X)] and CreatedAtAction(RouteNames.X, ...). A rename becomes one const edit. The Plexor codebase is already mid-migration -- IAM controllers and WorkloadsController previously had this exact violation (fixed in commits db19917, 0213f48); ClustersController and AuthController still use literal strings in [Http*] attributes. Glob is restricted to *Controller.cs to avoid false positives on ConfigureHostOptions / IHostBuilder / etc. which also take Name arguments.',
};
