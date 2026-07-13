// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TokenIssuer — Plexor-aware adapter over IJwtSigningService. Resolves
// permission claims via IPermissionResolver and signs the principal.
// ============================================================================

using System.Security.Claims;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     <see cref="ITokenIssuer" /> implementation. Reads the user's
///     permissions from the resolver, assembles the canonical
///     <see cref="ClaimsPrincipal" /> with multiple <c>role</c> and
///     <c>permission</c> claims, then delegates signing to
///     <see cref="IJwtSigningService" />.
/// </summary>
/// <param name="permissions"></param>
/// <param name="signing"></param>
/// <remarks>
///     <para><b>Multiple role claims.</b> <see cref="ClaimsIdentity" />
///     allows multiple claims with the same type — each role gets its
///     own <c>role</c> claim, each permission gets its own
///     <c>permission</c> claim. ASP.NET Core's authorization pipeline
///     reads role claims via <c>ClaimTypes.Role</c>; Plexor's shared
///     authorization handler (Phase 3.7) reads permission claims via
///     <c>permission</c>.</para>
/// </remarks>
public sealed class TokenIssuer(
    IPermissionResolver permissions,
    IJwtSigningService signing) : ITokenIssuer
{
    /// <inheritdoc />
    public async Task<IssuedAccessToken> IssueAsync(
        Guid userId,
        Guid orgId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(orgId, Guid.Empty);

        var resolvedPermissions = await permissions.ResolveAsync(
            userId, orgId, cancellationToken);

        var claims = new List<Claim>
        {
            new(IdentityClaims.UserId, userId.ToString()),
            new(IdentityClaims.TenantId, orgId.ToString()),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(IdentityClaims.Roles, role));
        }

        foreach (var permission in resolvedPermissions)
        {
            claims.Add(new Claim(IdentityClaims.Permission, permission));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "PlexorBearer"));
        return await signing.IssueAsync(principal, cancellationToken);
    }
}
