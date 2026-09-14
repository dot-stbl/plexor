// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BearerAuthenticationHandlerHelpers — pure-function helpers for
// BearerAuthenticationHandler. Satisfies code-shape.md §9 (no private
// methods in production classes) by lifting the API-key shape parser
// AND the AuthResolution → ClaimsPrincipal claim-builder out to
// file-static functions.
// ============================================================================

using System.Security.Claims;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.AuthProviders;
using Plexor.Modules.Sigil.Infrastructure.CurrentUser;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Static helpers for <see cref="BearerAuthenticationHandler" />.
///     No per-instance state; pure functions over strings, claims,
///     and <see cref="AuthResolution" />.
/// </summary>
internal static class BearerAuthenticationHandlerHelpers
{
    /// <summary>
    ///     Parse <c>kid_&lt;uuid&gt;.&lt;secret&gt;</c> into its two
    ///     parts. <c>kid_</c> prefix is required; UUID must be a real
    ///     <see cref="Guid" />; the secret is the raw base64url
    ///     portion after the dot. Returns <c>false</c> for anything
    ///     that doesn't match — the caller maps that to a generic
    ///     invalid-token failure.
    /// </summary>
    /// <param name="rawToken">The full <c>Authorization: Bearer</c>
    ///     credential (after the <c>Bearer </c> prefix).</param>
    /// <param name="keyId">Parsed <c>kid_</c> portion as a
    ///     <see cref="Guid" />. <see cref="Guid.Empty" /> on
    ///     <c>false</c> return.</param>
    /// <param name="secret">Parsed secret portion (raw, un-decoded).
    ///     Empty on <c>false</c> return.</param>
    public static bool TryParseApiKeyToken(
        string rawToken,
        out Guid keyId,
        out string secret)
    {
        keyId = Guid.Empty;
        secret = string.Empty;

        const string kidPrefix = "kid_";
        if (!rawToken.StartsWith(kidPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var dotIndex = rawToken.IndexOf('.');
        if (dotIndex <= kidPrefix.Length)
        {
            return false;
        }

        var kidString = rawToken[kidPrefix.Length..dotIndex];
        if (!Guid.TryParse(kidString, out keyId) || keyId == Guid.Empty)
        {
            return false;
        }

        secret = rawToken[(dotIndex + 1)..];
        return secret.Length > 0;
    }

    /// <summary>
    ///     Build the canonical <see cref="ClaimsPrincipal" /> from a
    ///     resolved <see cref="AuthResolution" />. The claim shape
    ///     mirrors what <c>TokenIssuer.BuildPrincipal</c> writes for
    ///     Sigil-issued JWTs (<c>sub</c> / <c>tid</c> / <c>role</c>[] /
    ///     <c>permission</c>[]) plus the provider discriminator on
    ///     <c>iss</c> and the <c>service</c> flag the
    ///     <see cref="HttpContextCurrentUser" /> reads to set
    ///     <c>IsService</c>.
    /// </summary>
    /// <remarks>
    ///     <para>Drift on any of these claim types breaks the
    ///     authorization pipeline silently — <c>[RequirePermission]</c>
    ///     reads <c>permission</c>, <c>ICurrentUser.Permissions</c>
    ///     reads the same. Always reference the constants in
    ///     <see cref="IdentityClaims" />; literal strings here would
    ///     drift the moment the constant changes.</para>
    /// </remarks>
    /// <param name="resolution">Resolution returned by the auth
    ///     provider resolver.</param>
    /// <param name="authenticationType">
    ///     Value passed to the underlying
    ///     <see cref="ClaimsIdentity(string)" /> — surfaced as
    ///     <c>Identity.AuthenticationType</c> and used by tests +
    ///     audit. The handler passes
    ///     <see cref="BearerOptions.SchemeName" />.</param>
    public static ClaimsPrincipal BuildPrincipal(
        AuthResolution resolution,
        string authenticationType)
    {
        var claims = new List<Claim>
        {
            new(IdentityClaims.UserId, resolution.UserId.ToString()),
            new(IdentityClaims.TenantId, resolution.OrgId.ToString()),
            new(IdentityClaims.Issuer, resolution.ProviderId.Value),
            new(IdentityClaims.IsService, resolution.IsService ? "true" : "false"),
        };

        foreach (var role in resolution.Roles)
        {
            claims.Add(new Claim(IdentityClaims.Roles, role));
        }

        foreach (var permission in resolution.Permissions)
        {
            claims.Add(new Claim(IdentityClaims.Permission, permission));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType));
    }
}

