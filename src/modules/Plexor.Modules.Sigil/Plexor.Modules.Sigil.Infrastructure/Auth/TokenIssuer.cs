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

        var resolvedPermissions = await permissions.ResolveAsync(
            userId, orgId, cancellationToken);

        var principal = BuildPrincipal(userId, orgId, roles, resolvedPermissions);
        return await signing.IssueAsync(principal, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IssuedAccessToken> IssueWithOverrideAsync(
        Guid userId,
        Guid orgId,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> overridePermissions,
        TimeSpan overrideLifetime,
        CancellationToken cancellationToken = default)
    {

        var principal = BuildPrincipal(userId, orgId, roles, overridePermissions);
        return await signing.IssueWithLifetimeAsync(
            principal, overrideLifetime, cancellationToken);
    }

    /// <summary>
    ///     Build the canonical <see cref="ClaimsPrincipal" /> with
    /// the caller identity, organisation, role names, and permission
    /// strings. Used by both <see cref="IssueAsync" /> (which passes
    /// role-resolved permissions) and
    /// <see cref="IssueWithOverrideAsync" /> (which passes caller-
    /// supplied permissions during the password-change path).
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="orgId"></param>
    /// <param name="roles"></param>
    /// <param name="permissionsToBake"></param>
    private static ClaimsPrincipal BuildPrincipal(
        Guid userId,
        Guid orgId,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissionsToBake)
    {
        var claims = new List<Claim>
        {
            new(IdentityClaims.UserId, userId.ToString()),
            new(IdentityClaims.TenantId, orgId.ToString()),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(IdentityClaims.Roles, role));
        }

        foreach (var permission in permissionsToBake)
        {
            claims.Add(new Claim(IdentityClaims.Permission, permission));
        }

        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticationType: "PlexorBearer"));
    }
}
