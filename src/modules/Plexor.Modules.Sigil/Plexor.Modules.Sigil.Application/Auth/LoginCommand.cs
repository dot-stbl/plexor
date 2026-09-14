// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoginCommand + LoginResult — password grant issuance flow. Handler
// lives in Infrastructure/Auth (depends on DbContext + IPasswordHasher
// + IRefreshTokenStore + ITokenIssuer).
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Password-grant login. Verifies credentials against
///     <c>sigil.users.password_hash</c>, increments the failed-login
///     counter on miss, issues a fresh access + refresh pair on
///     success. The two identification fields are both optional —
///     exactly one must be supplied (the handler picks the lookup
///     strategy based on which is set).
/// </summary>
/// <param name="OrgId">Tenant scope. The login is org-scoped (a
///     user belongs to exactly one org in v0.1).</param>
/// <param name="Email">
///     Email address. Mutually exclusive with <paramref name="Username" />.
///     Compared case-insensitively against the stored email.
/// </param>
/// <param name="Username">
///     Username (email local-part, no domain). Mutually exclusive
///     with <paramref name="Email" />.
/// </param>
/// <param name="Password">Plain-text password. Never logged.</param>
/// <param name="RedirectPath">
///     Optional operator-supplied deep-link path that the login
///     screen wants the OIDC redirect to return to. Used by the
///     handler to compose the <c>redirect</c> extension on the
///     provider-mismatch response — the console forwards it to
///     <c>POST /auth/oidc/authorize</c> as its <c>redirect</c>
///     query param. Defaults to <c>"/console"</c> when the
///     controller didn't surface a query parameter.</param>
public sealed record LoginCommand(
    Guid OrgId,
    string? Email,
    string? Username,
    string Password,
    string RedirectPath = "/console");
