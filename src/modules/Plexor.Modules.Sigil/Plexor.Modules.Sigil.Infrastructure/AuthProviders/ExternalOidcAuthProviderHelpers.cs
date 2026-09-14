// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ExternalOidcAuthProviderHelpers — pure-function helpers pulled out of
// ExternalOidcAuthProvider to satisfy the no-private-methods convention
// (code-shape.md §9 / class-layout-and-tooling.md §1a). The provider
// class is the orchestrator; the deterministic user-id derivation
// (SHA-256 over (issuer|sub)), the external-claim → role-name
// extraction, and the token-shape validation primitives (read /
// signing-key lookup / validate) live here as file-static methods.
// ============================================================================

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders;

/// <summary>
///     Static helpers for <see cref="ExternalOidcAuthProvider" />.
///     No per-instance state; all methods are pure (or pure with
///     transient object lifetimes — e.g. <see cref="JsonWebTokenHandler" />
///     per call, which is the canonical Microsoft pattern).
/// </summary>
internal static class ExternalOidcAuthProviderHelpers
{
    /// <summary>
    ///     Derive a Plexor-side user id from the external
    ///     <c>(issuer, sub)</c> pair. Same external user
    ///     (same issuer URL + same <c>sub</c> claim) → same Plexor
    ///     user id across logins, which keeps audit history +
    ///     role-binding shape consistent with the Sigil path.
    /// </summary>
    /// <remarks>
    ///     <para><b>Why SHA-256, not RFC 9562 v5 (SHA-1).</b>
    ///     Both produce a 16-byte hash → <see cref="Guid" />. The
    ///     task description called this "GUID v5"; the concrete
    ///     hash choice doesn't matter for the API contract
    ///     (deterministic + 128-bit), but SHA-256 is the project's
    ///     modern default and avoids the SHA-1 dependency.</para>
    ///     <para><b>Why a domain separator.</b> The
    ///     <c>{issuer}|{subject}</c> format with a pipe separator
    ///     prevents collisions across IDPs: <c>iss=a|sub=b</c> and
    ///     <c>iss=a|b|sub=...</c> would otherwise hash to the same
    ///     bucket for some pathological inputs. A pipe is a
    ///     non-character in every URL/UUID/sub combination we
    ///     expect.</para>
    /// </remarks>
    /// <param name="issuer">The OIDC <c>iss</c> claim.</param>
    /// <param name="subject">The OIDC <c>sub</c> claim.</param>
    /// <returns>The deterministic Plexor user id.</returns>
    public static Guid DeterministicUserId(string issuer, string subject)
    {
        var bytes = Encoding.UTF8.GetBytes($"{issuer}|{subject}");
        var hash = SHA256.HashData(bytes);
        return new Guid(hash.Take(16).ToArray());
    }

    /// <summary>
    ///     Pull external role names from the validated JWT claims.
    ///     Three claim shapes are supported in v0.1:
    ///     <list type="bullet">
    ///       <item><c>roles</c> — generic single-value role claim
    ///       (RFC 9068 access tokens).</item>
    ///       <item><c>realm_access.roles</c> — Keycloak's nested
    ///       JSON-array claim. The claim value is itself a
    ///       JSON-encoded array string, not an array of individual
    ///       claim values; the helper parses the string.</item>
    ///       <item><c>https://plexor.example.com/roles</c> —
    ///       vendor-specific Plexor claim for tokens minted by an
    ///       internal IDP that follows the Plexor convention.</item>
    ///     </list>
    /// </summary>
    /// <param name="claims">The validated JWT claim set.</param>
    /// <returns>
    ///     A deduplicated role-name set. Order is unspecified;
    ///     comparison is ordinal. Empty when no recognised claim
    ///     type carries roles.
    /// </returns>
    public static IReadOnlyCollection<string> ExtractExternalRoles(IEnumerable<Claim> claims)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var claim in claims)
        {
            if (string.IsNullOrWhiteSpace(claim.Value))
            {
                continue;
            }

            switch (claim.Type)
            {
                case "roles":
                case "https://plexor.example.com/roles":
                    result.Add(claim.Value);
                    break;
                case "realm_access.roles":
                    // Keycloak shape — JSON-encoded array string.
                    // Use the framework's shared JsonSerializerOptions.Web
                    // (anti-patterns.md §6) instead of an inline options
                    // instance. Bad JSON in a single claim must not
                    // fail the whole resolution — drop the malformed
                    // claim, keep the rest.
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<string[]>(
                            claim.Value,
                            JsonSerializerOptions.Web);
                        if (parsed is not null)
                        {
                            foreach (var role in parsed)
                            {
                                if (!string.IsNullOrWhiteSpace(role))
                                {
                                    result.Add(role);
                                }
                            }
                        }
                    }
                    catch (JsonException)
                    {
                    }

                    break;
            }
        }

        return result.ToArray();
    }

    /// <summary>
    ///     Read the JWT header + body without signature verification.
    ///     Returns <c>null</c> for malformed input (wrong shape,
    ///     bad base64url, etc.).
    /// </summary>
    /// <param name="rawCredential">The compact JWT string.</param>
    public static (JsonWebTokenHandler Handler, JsonWebToken? Token) ReadToken(string rawCredential)
    {
        if (string.IsNullOrWhiteSpace(rawCredential))
        {
            return (new JsonWebTokenHandler(), null);
        }

        var handler = new JsonWebTokenHandler();
        try
        {
            var token = handler.ReadJsonWebToken(rawCredential);
            return (handler, token);
        }
        catch (ArgumentException)
        {
            return (handler, null);
        }
    }

    /// <summary>
    ///     Find the JWKS entry whose <c>kid</c> matches the inbound
    ///     token's <c>kid</c> header. Returns <c>null</c> when no
    ///     match (kid missing from JWKS — caller logs + treats as
    ///     "this provider doesn't claim this credential").
    /// </summary>
    /// <param name="keySet">The JWKS for the configured authority.</param>
    /// <param name="kid">The inbound JWT's <c>kid</c> header (may be
    /// <c>null</c> if the IDP omits it).</param>
    public static SecurityKey? ResolveSigningKey(JsonWebKeySet keySet, string? kid)
    {
        if (string.IsNullOrEmpty(kid))
        {
            // Some IDPs (notably older Auth0 setups) don't emit kid.
            // Accept the first signing key in that case — single-key
            // operators are the common shape.
            return keySet.GetSigningKeys().FirstOrDefault();
        }

        foreach (var key in keySet.GetSigningKeys())
        {
            if (string.Equals(key.KeyId, kid, StringComparison.Ordinal))
            {
                return key;
            }
        }

        return null;
    }

    /// <summary>
    ///     Build <see cref="TokenValidationParameters" /> from the
    ///     per-tenant config and run
    ///     <see cref="JsonWebTokenHandler.ValidateTokenAsync(string, TokenValidationParameters)" />.
    ///     All four checks (issuer, audience, lifetime, signing key)
    ///     are enabled — a misconfigured tenant must not let an
    ///     attacker trade a Sigil token for an OIDC acceptance.
    /// </summary>
    /// <param name="handler">Reused <see cref="JsonWebTokenHandler" /> from the read step.</param>
    /// <param name="rawCredential">The compact JWT.</param>
    /// <param name="validIssuer">Per-tenant OIDC issuer (authority URL).</param>
    /// <param name="validAudience">Per-tenant OIDC client id.</param>
    /// <param name="signingKey">The resolved JWKS signing key.</param>
    /// <param name="clockSkew">Tolerance for <c>exp</c> / <c>nbf</c> checks.</param>
    public static async Task<TokenValidationResult> ValidateAsync(
        JsonWebTokenHandler handler,
        string rawCredential,
        string validIssuer,
        string validAudience,
        SecurityKey signingKey,
        TimeSpan clockSkew)
    {
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = validIssuer,
            ValidAudience = validAudience,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = clockSkew,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            NameClaimType = "sub",
        };

        return await handler.ValidateTokenAsync(rawCredential, parameters);
    }
}
