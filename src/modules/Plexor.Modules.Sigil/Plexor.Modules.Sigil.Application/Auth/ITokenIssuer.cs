// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ITokenIssuer — issues an access token (signed JWT) for an authenticated
// user. Resolves the user's permission set via IPermissionResolver and
// bakes the resulting claims into the token so per-request authorize
// checks don't need to hit the DB.
// ==========================================================================

using System.Security.Claims;

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Issues access tokens with role + permission claims pre-resolved
/// against the database. Bridges <see cref="IPermissionResolver" /> and
/// <see cref="IJwtSigningService" />.
/// </summary>
/// <remarks>
///     <para><b>Multiple role claims.</b> <see cref="ClaimsIdentity" />
/// allows multiple claims with the same type — each role gets its
/// own <c>role</c> claim, each permission gets its own
/// <c>permission</c> claim. ASP.NET Core's authorization pipeline
/// reads role claims via <c>ClaimTypes.Role</c>; Plexor's shared
/// authorization handler (Phase 3.7) reads permission claims via
/// <c>permission</c>.</para>
///     <para><b>Caller identity.</b> The userId + orgId parameters
/// come from the verified login (or the rotating refresh) — never
/// from caller-supplied headers.</para>
/// </remarks>
public interface ITokenIssuer
{
    /// <summary>
    ///     Issue a fresh access token. Resolves permissions from the DB,
    /// builds the canonical claims principal, and signs it via the
    /// underlying <see cref="IJwtSigningService" />.
    /// </summary>
    /// <param name="userId">Caller identity (sigil.users.id).</param>
    /// <param name="orgId">Tenant scope (sigil.users.org_id).</param>
    /// <param name="roles">Role names bound to the caller.</param>
    /// <param name="cancellationToken">Forwarded to permission resolution.</param>
    /// <returns>The signed access JWT + its expiry instant.</returns>
    public Task<IssuedAccessToken> IssueAsync(
        Guid userId,
        Guid orgId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Issue an access token whose permission set is **overridden**
    /// to <paramref name="overridePermissions" /> (e.g. the single
    /// <c>iam.users.change-own-password</c> permission used during
    /// the first-login password rotation) and whose lifetime is
    /// shortened to <paramref name="overrideLifetime" />. Used when
    /// the caller has <c>MustChangePassword = true</c> — they're
    /// authenticated, but should only be able to call one endpoint
    /// until they rotate.
    /// </summary>
    /// <param name="userId">Caller identity.</param>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="roles">Role names to bake (may be empty for the
    /// password-change path).</param>
    /// <param name="overridePermissions">Permissions to bake in place
    /// of the role-resolved set.</param>
    /// <param name="overrideLifetime">Access-token TTL. Caller picks
    /// (typically 5 min for password reset).</param>
    /// <param name="cancellationToken">Forwarded to signing.</param>
    /// <returns>The signed access JWT + its expiry instant.</returns>
    public Task<IssuedAccessToken> IssueWithOverrideAsync(
        Guid userId,
        Guid orgId,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> overridePermissions,
        TimeSpan overrideLifetime,
        CancellationToken cancellationToken = default);
}
