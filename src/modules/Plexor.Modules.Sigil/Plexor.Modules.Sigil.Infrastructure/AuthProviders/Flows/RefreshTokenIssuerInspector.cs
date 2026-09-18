// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RefreshTokenIssuerInspector — peek the JWT `iss` claim off a refresh
// token WITHOUT verifying its signature. The router for POST /auth/refresh
// (Phase 4.6.3c) keys off this peek to dispatch Sigil vs OIDC refresh
// paths.
//
// Why a separate helper: the resolver already has one
// (`AuthProviderResolverHelpers.PeekIssuer`), but that one swallows all
// parse failures and returns null. The refresh path needs to
// distinguish three shapes:
//   1. Empty / whitespace / opaque random — NOT a JWT, return null
//      so the Sigil path runs.
//   2. Looks like a JWT (≥ 2 dots) but won't parse — return a
//      failure sentinel so the handler emits 400
//      identity.refresh.malformed. Truly-broken input is treated
//      differently from "this isn't a JWT at all".
//   3. Real JWT — return the issuer (or null if no iss claim).
//
// The discriminator is structural: ≤ 1 dot → opaque; ≥ 2 dots →
// JWT-shaped. JsonWebTokenHandler confirms the rest by attempting to
// parse.
// ============================================================================

using Microsoft.IdentityModel.JsonWebTokens;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders.Flows;

/// <summary>
///     Outcome of peeking the <c>iss</c> claim off a refresh token
///     string. The handler dispatches Sigil vs OIDC vs malformed
///     on the three cases.
/// </summary>
/// <param name="Issuer">The <c>iss</c> claim value. <c>null</c> when
/// the input is opaque (not a JWT) or when the JWT has no
/// <c>iss</c> claim.</param>
/// <param name="IsMalformed"><c>true</c> when the input looks like a
/// JWT (has at least two dots) but <see cref="JsonWebTokenHandler" />
/// failed to parse it. The handler turns this into 400
/// <c>identity.refresh.malformed</c>.</param>
public readonly record struct RefreshTokenIssuerPeek(string? Issuer, bool IsMalformed)
{
    /// <summary>Opaque / empty / non-JWT input — no <c>iss</c>
    /// available. The handler should treat this as the Sigil path
    /// (backward-compat with the v0.1 opaque random refresh tokens).</summary>
    public static RefreshTokenIssuerPeek Opaque { get; } = new(null, false);

    /// <summary>JWT that looks well-formed but lacks an <c>iss</c>
    /// claim. Treated as Sigil path (same as Opaque).</summary>
    public static RefreshTokenIssuerPeek JwtWithoutIssuer { get; } = new(null, false);

    /// <summary>JWT with the given <c>iss</c> claim.</summary>
    /// <param name="issuer">Claim value.</param>
    public static RefreshTokenIssuerPeek WithIssuer(string issuer)
    {
        return new RefreshTokenIssuerPeek(issuer, false);
    }

    /// <summary>Input looks like a JWT but won't parse. Caller
    /// surfaces 400.</summary>
    public static RefreshTokenIssuerPeek Malformed { get; } = new(null, true);
}

/// <summary>
///     Static helpers for the Sigil-vs-OIDC refresh routing decision
///     in <c>RefreshCommandHandler</c> — peeks the <c>iss</c> claim
///     off a refresh token string. Stateless, pure; uses a
///     transient <see cref="JsonWebTokenHandler" /> per call
///     (canonical Microsoft pattern).
/// </summary>
internal static class RefreshTokenIssuerInspector
{
    /// <summary>
    ///     Peek the JWT <c>iss</c> claim from a refresh token string
    ///     without verifying its signature. Distinguishes three input
    ///     shapes (see <see cref="RefreshTokenIssuerPeek" />):
    ///     empty / opaque → <see cref="RefreshTokenIssuerPeek.Opaque" />,
    ///     malformed JWT → <see cref="RefreshTokenIssuerPeek.Malformed" />,
    ///     well-formed JWT → <see cref="RefreshTokenIssuerPeek.WithIssuer" />
    ///     or <see cref="RefreshTokenIssuerPeek.JwtWithoutIssuer" />.
    /// </summary>
    /// <remarks>
    ///     <para><b>Why structural dot-count.</b> The default
    ///     behaviour of <c>JsonWebTokenHandler.ReadJsonWebToken</c>
    ///     is to throw <see cref="ArgumentException" /> for inputs
    ///     that don't look like a JWT (no dots). We catch that
    ///     exception to detect "not a JWT at all", but we
    ///     additionally check the dot-count so we can distinguish
    ///     opaque-but-valid (random base64url) from
    ///     looks-like-a-JWT-but-broken (binary garbage with dots).
    ///     The handler maps the latter to 400
    ///     <c>identity.refresh.malformed</c>.</para>
    ///     <para><b>Why no signature check.</b> The inspector is the
    ///     dispatcher — it only routes. Full validation lives in the
    ///     downstream handler. The inspector does <em>not</em>
    ///     trust the issuer value; the refresh rotation in
    ///     <c>EfRefreshTokenStore</c> treats the stored hash as the
    ///     source of truth.</para>
    /// </remarks>
    /// <param name="rawToken">Refresh token string from the
    /// <c>POST /auth/refresh</c> body.</param>
    public static RefreshTokenIssuerPeek PeekIssuer(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return RefreshTokenIssuerPeek.Opaque;
        }

        var dotCount = CountDots(rawToken);
        if (dotCount < 2)
        {
            // ≤ 1 dot: can't be a JWT (which is header.payload.signature).
            // Treat as opaque — the Sigil refresh path handles it.
            return RefreshTokenIssuerPeek.Opaque;
        }

        try
        {
            var handler = new JsonWebTokenHandler();
            var token = handler.ReadJsonWebToken(rawToken);
            return string.IsNullOrEmpty(token.Issuer)
                ? RefreshTokenIssuerPeek.JwtWithoutIssuer
                : RefreshTokenIssuerPeek.WithIssuer(token.Issuer);
        }
        catch (ArgumentException)
        {
            return RefreshTokenIssuerPeek.Malformed;
        }
    }

    /// <summary>
    ///     Count the dots in a raw token string without allocating.
    ///     Two dots is the minimum for a compact JWS (header.payload.signature);
    ///     JWE has 5 segments (4 dots).
    /// </summary>
    /// <param name="rawToken">Input string.</param>
    private static int CountDots(string rawToken)
    {
        var count = 0;
        foreach (var ch in rawToken)
        {
            if (ch == '.')
            {
                count++;
            }
        }

        return count;
    }
}
