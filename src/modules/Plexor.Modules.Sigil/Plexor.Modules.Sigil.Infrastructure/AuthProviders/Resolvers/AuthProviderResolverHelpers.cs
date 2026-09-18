// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthProviderResolverHelpers — pure-function helpers for
// AuthProviderResolver. Satisfies code-shape.md §9 (no private methods in
// production classes) by lifting the JWT peek + cache-key composition
// out to file-static functions.
// ============================================================================

using Microsoft.IdentityModel.JsonWebTokens;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders.Resolvers;

/// <summary>
///     Static helpers for <see cref="AuthProviderResolver" />. No
///     per-instance state; all methods are pure or use transient object
///     lifetimes (the <see cref="JsonWebTokenHandler" /> is per-call —
///     canonical Microsoft pattern).
/// </summary>
internal static class AuthProviderResolverHelpers
{
    /// <summary>
    ///     Peek the JWT <c>iss</c> claim without validating the
    ///     signature. Returns <c>null</c> for any malformed input
    ///     (non-JWT shape, bad base64url, missing <c>iss</c>).
    /// </summary>
    /// <remarks>
    ///     <para><b>Why no signature check.</b> The resolver is the
    ///     dispatcher — it routes the credential to the right provider
    ///     based on its <c>iss</c> shape. The provider then performs
    ///     the full signature + claim + lifetime validation against
    ///     its own key material. A signature-mismatch failure in the
    ///     provider surfaces as <c>null</c> at the resolver level.</para>
    ///     <para><b>Why <see cref="JsonWebTokenHandler" />.</b>
    ///     Mirrors <see cref="Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc.ExternalOidcAuthProviderHelpers.ReadToken" />:
    ///     modern Microsoft pipeline, raw claim names preserved
    ///     (<c>iss</c> rather than the
    ///     <see cref="System.Security.Claims.ClaimTypes" /> mapping that
    ///     <c>JwtSecurityTokenHandler</c> does).</para>
    /// </remarks>
    /// <param name="rawCredential">Compact JWT string from the
    /// <c>Authorization: Bearer</c> header.</param>
    public static string? PeekIssuer(string rawCredential)
    {
        if (string.IsNullOrWhiteSpace(rawCredential))
        {
            return null;
        }

        try
        {
            var handler = new JsonWebTokenHandler();
            var token = handler.ReadJsonWebToken(rawCredential);
            return string.IsNullOrEmpty(token.Issuer) ? null : token.Issuer;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
