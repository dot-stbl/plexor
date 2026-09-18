// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfOidcIdTokenValidator — IOidcIdTokenValidator implementation.
// Validates an id_token JWT returned by the OIDC token endpoint
// against the per-tenant OidcTenantConfig projection (authority,
// JWKS, client id / audience). Returns the lifted claims via
// IdTokenClaimsExtractor.
//
// The validation primitives (read / signing-key lookup /
// TokenValidationParameters + ValidateTokenAsync) live in
// ExternalOidcAuthProviderHelpers (file-static). This class wires
// them together for the callback path: get JWKS by authority →
// resolve signing key by kid → run the validation → extract
// claims. Same algorithm as ExternalOidcAuthProvider; different
// call cadence (one-off per OIDC roundtrip vs. per-HTTP-request).
// ============================================================================

using Microsoft.Extensions.Logging;
using Plexor.Modules.Sigil.Application.AuthProviders;

namespace Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc;

/// <summary>
///     <see cref="IOidcIdTokenValidator" /> implementation.
///     Validates an id_token against the per-tenant
///     <see cref="OidcTenantConfig" /> + JWKS, then extracts
///     the user-identity claims via <see cref="IdTokenClaimsExtractor" />.
/// </summary>
/// <remarks>
///     <para><b>Scoped lifetime.</b> Stateless beyond the
///     <see cref="IJwksFetcher" /> cache (which is itself a
///     process-wide singleton). Same shape as
///     <see cref="ExternalOidcAuthProvider" />.</para>
///     <para><b>Why no log of failure reason.</b> The reason for
///     a failed validation may carry PII (the raw JWT in
///     <c>ex.Message</c>); we log the structured fields
///     (authority, kid) and skip the message. The bearer-side
///     provider logs the message at Information level — that path
///     has already validated the input shape and is safe to log.</para>
/// </remarks>
/// <param name="jwksFetcher">Resolves the JWKS for the
/// configured authority (cached 1h, fetched on miss).</param>
/// <param name="logger">Structured logger.</param>
public sealed class EfOidcIdTokenValidator(
    IJwksFetcher jwksFetcher,
    ILogger<EfOidcIdTokenValidator> logger) : IOidcIdTokenValidator
{
    /// <summary>
    ///     Tolerance window for <c>exp</c> / <c>nbf</c> checks
    ///     against the system clock. 1 minute is the standard
    ///     OIDC recommendation (RFC 7519 §4.1.4).
    /// </summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    public async Task<OidcIdTokenClaims?> ValidateAsync(
        string idToken,
        OidcTenantConfig config,
        CancellationToken cancellationToken = default)
    {
        var (handler, token) = ExternalOidcAuthProviderHelpers.ReadToken(idToken);
        if (token is null)
        {
            logger.LogWarning(
                "EfOidcIdTokenValidator: id_token is not a well-formed JWT.");
            return null;
        }

        if (string.IsNullOrEmpty(config.Authority))
        {
            logger.LogWarning(
                "EfOidcIdTokenValidator: org {OrgId} has no Authority",
                config.OrgId);
            return null;
        }

        if (string.IsNullOrEmpty(config.ClientId))
        {
            logger.LogWarning(
                "EfOidcIdTokenValidator: org {OrgId} has no ClientId",
                config.OrgId);
            return null;
        }

        if (!string.Equals(token.Issuer, config.Authority, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "EfOidcIdTokenValidator: id_token issuer {Issuer} does not match config authority {Authority}",
                token.Issuer,
                config.Authority);
            return null;
        }

        var keySet = await jwksFetcher.GetKeySetAsync(config.Authority, cancellationToken);

        var signingKey = ExternalOidcAuthProviderHelpers.ResolveSigningKey(keySet, token.Kid);
        if (signingKey is null)
        {
            logger.LogWarning(
                "EfOidcIdTokenValidator: kid {Kid} not in JWKS for {Authority}",
                token.Kid,
                config.Authority);
            return null;
        }

        var validation = await ExternalOidcAuthProviderHelpers.ValidateAsync(
            handler,
            idToken,
            config.Authority,
            config.ClientId,
            signingKey,
            ClockSkew);

        if (!validation.IsValid)
        {
            logger.LogInformation(
                "EfOidcIdTokenValidator: id_token validation failed for {Authority}",
                config.Authority);
            return null;
        }

        try
        {
            return IdTokenClaimsExtractor.Extract(token);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(
                "EfOidcIdTokenValidator: id_token passed validation but claim extraction failed: {Reason}",
                ex.Message);
            return null;
        }
    }
}
