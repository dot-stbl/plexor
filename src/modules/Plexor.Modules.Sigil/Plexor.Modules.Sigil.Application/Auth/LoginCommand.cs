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
public sealed record LoginCommand(
    Guid OrgId,
    string? Email,
    string? Username,
    string Password);
