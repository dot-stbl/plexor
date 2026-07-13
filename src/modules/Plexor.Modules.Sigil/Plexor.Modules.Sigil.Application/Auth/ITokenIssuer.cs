// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ITokenIssuer — issues an access token (signed JWT) for an authenticated
// user. Resolves the user's permission set via IPermissionResolver and
// bakes the resulting claims into the token so per-request authorize
// checks don't need to hit the DB.
// ============================================================================

using System.Security.Claims;

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Issues access tokens with role + permission claims pre-resolved
///     against the database. Bridges <see cref="IPermissionResolver" />
///     and <see cref="IJwtSigningService" />.
/// </summary>
/// <remarks>
///     <para><b>Why a separate issuer.</b> <see cref="IJwtSigningService" />
///     operates on arbitrary <see cref="ClaimsPrincipal" />s — it
///     doesn't know about Plexor's identity model. The issuer is the
///     Plexor-aware adapter that:
///     <list type="number">
///       <item>Resolves the user's permission strings from
///       <see cref="IPermissionResolver" />.</item>
///       <item>Builds the <see cref="ClaimsPrincipal" /> with the
///       canonical <see cref="Abstractions.IdentityClaims" /> values
///       (<c>sub</c>, <c>tid</c>, <c>role</c> multiple, <c>permission</c>
///       multiple).</item>
///       <item>Delegates signing to <see cref="IJwtSigningService" />.</item>
///     </list></para>
///     <para><b>Caller identity.</b> The userId and orgId parameters come
///     from the verified login (or the rotating refresh) — never from
///     caller-supplied headers.</para>
/// </remarks>
public interface ITokenIssuer
{
    /// <summary>
    ///     Issue a fresh access token. Resolves permissions from the DB,
    ///     builds the canonical claims principal, and signs it via the
    ///     underlying <see cref="IJwtSigningService" />.
    /// </summary>
    /// <param name="userId">Caller identity (sigil.users.id).</param>
    /// <param name="orgId">Tenant scope (sigil.users.org_id).</param>
    /// <param name="roles">Role names bound to the caller (denormalized
    /// at sign time so refresh can issue tokens without re-resolving
    /// role_bindings).</param>
    /// <param name="cancellationToken">Forwarded to permission resolution.</param>
    /// <returns>The signed access JWT + its expiry instant.</returns>
    public Task<IssuedAccessToken> IssueAsync(
        Guid userId,
        Guid orgId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);
}
