// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IdTokenClaimsExtractor — pure-function helper that lifts the
// `sub` / `email` / `preferred_username` / `name` claims out of a
// validated JWT (parsed JsonWebToken) into the structured
// OidcIdTokenClaims carrier. Phase 4.6.3c.
//
// Co-located with ExternalOidcAuthProviderHelpers because it shares
// the same JWT-handling primitives; sits in the same AuthProviders
// folder because callers (EfOidcIdTokenValidator) live one layer up
// in the same assembly. File-static so the helper doesn't pollute
// the public surface (code-shape.md §9.10).
// ============================================================================

using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Plexor.Modules.Sigil.Application.AuthProviders;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc;

/// <summary>
///     Pure helpers for extracting the OIDC user-identity claims
///     from a parsed <see cref="JsonWebToken" />. Lifts the four
///     claims the callback handler needs (sub / email / preferred
///     username / name) into the structured carrier the
///     <see cref="IOidcUserProvisioner" /> consumes. No DI, no IO.
/// </summary>
internal static class IdTokenClaimsExtractor
{
    /// <summary>
    ///     Standard OIDC <c>sub</c> claim name. The validator
    ///     ensures this is non-null before returning; an absent
    ///     sub causes the validator to return null (caller → 400).
    /// </summary>
    private const string SubClaim = "sub";

    /// <summary>
    ///     Standard OIDC <c>email</c> claim name. Optional —
    ///     provisioner throws on null.
    /// </summary>
    private const string EmailClaim = "email";

    /// <summary>
    ///     OIDC <c>preferred_username</c> claim name. Optional —
    ///     preferred display-name source.
    /// </summary>
    private const string PreferredUsernameClaim = "preferred_username";

    /// <summary>
    ///     OIDC <c>name</c> claim name. Optional — secondary
    ///     display-name source.
    /// </summary>
    private const string NameClaim = "name";

    /// <summary>
    ///     Lift the OIDC identity claims off a validated JWT. The
    ///     <see cref="JsonWebToken" /> comes from
    ///     <see cref="Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc.ExternalOidcAuthProviderHelpers.ReadToken" />
    ///     after the validator's signature + lifetime checks pass.
    /// </summary>
    /// <param name="token">The validated JWT.</param>
    /// <returns>The populated claims carrier.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static OidcIdTokenClaims Extract(JsonWebToken token)
    {
        var sub = FindFirstValue(token.Claims, SubClaim);
        if (string.IsNullOrEmpty(sub))
        {
            // The validator guarantees this is non-null at the
            // point it returns the carrier; defensive check here
            // because the helper is public to the assembly.
            throw new InvalidOperationException(
                "Validated id_token has no `sub` claim.");
        }

        return new OidcIdTokenClaims(
            Issuer: token.Issuer,
            Subject: sub,
            Email: FindFirstValue(token.Claims, EmailClaim),
            PreferredUsername: FindFirstValue(token.Claims, PreferredUsernameClaim),
            Name: FindFirstValue(token.Claims, NameClaim));
    }

    /// <summary>
    ///     Return the value of the first claim whose type matches
    ///     <paramref name="type" />, or <c>null</c> when no such
    ///     claim is present.
    /// </summary>
    /// <param name="claims">The validated JWT's claim set.</param>
    /// <param name="type">The OIDC claim name (e.g. <c>sub</c>).</param>
    private static string? FindFirstValue(
        IEnumerable<Claim> claims,
        string type)
    {
        foreach (var claim in claims)
        {
            if (string.Equals(claim.Type, type, StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(claim.Value)
                    ? null
                    : claim.Value;
            }
        }

        return null;
    }
}
