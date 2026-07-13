// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IApiKeyAuthenticationService — service-to-service auth path. The
// BearerAuthenticationHandler routes "Authorization: Bearer kid_xxx.<secret>"
// values to this service instead of the JWT path. Returns a
// ClaimsPrincipal whose IsService = true and whose permission set
// matches the API key's pre-resolved scope.
// ==========================================================================

using System.Security.Claims;

namespace Plexor.Modules.Sigil.Application.Auth;

/// <summary>
///     Authenticate a Plexor API key. Called by the bearer
/// authentication handler (in Plexor.Modules.Sigil.Infrastructure)
/// when the <c>Authorization</c> header carries the API-key form
/// (<c>Bearer kid_xxx.&lt;secret&gt;</c>) rather than a JWT.
/// </summary>
/// <remarks>
///     <para><b>Why a separate service.</b> JWT validation is a
///     signature + claim-shape question; API key validation is a DB
///     lookup + SHA-256 hash question. Two different code paths, two
///     different failure modes — keeping them in separate handlers
///     lets each report its own <c>WWW-Authenticate: error="..."</c>
///     reason to the client.</para>
///     <para><b>Secret storage.</b> The raw secret is never persisted
///     — only the SHA-256 hash on <see cref="Domain.Entities.ApiKey.SecretHash" />.
///     Lookup-by-kid returns the hash; this service compares the
///     supplied secret against it in constant time
///     (<c>CryptographicOperations.FixedTimeEquals</c>).</para>
///     <para><b>Expiry / revocation.</b> The handler reads
///     <see cref="Domain.Entities.ApiKey.ExpiresAt" /> and
///     <see cref="Domain.Entities.ApiKey.RevokedAt" />; either set
///     rejects the key. Refresh-token revocation is a separate
///     concern — the key is the only "session" for a service
///     account.</para>
/// </remarks>
public interface IApiKeyAuthenticationService
{
    /// <summary>
    ///     Validate the presented API key. Returns
    /// <see cref="ApiKeyAuthenticationResult.Success" /> on a match,
    /// <see cref="ApiKeyAuthenticationResult.Invalid" /> on a hash
    /// mismatch, and <see cref="ApiKeyAuthenticationResult.NotFound" />
    /// when the <c>kid</c> is unknown.
    /// </summary>
    /// <param name="keyId">UUID v7 of the API key (parsed from
    /// <c>kid_xxx</c> in the bearer header).</param>
    /// <param name="rawSecret">Raw secret portion (the
    /// 43-char base64url part after the dot).</param>
    /// <param name="cancellationToken">Forwarded to the DB lookup.</param>
    public Task<ApiKeyAuthenticationResult> AuthenticateAsync(
        Guid keyId,
        string rawSecret,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Discriminated outcome of
/// <see cref="IApiKeyAuthenticationService.AuthenticateAsync" />.
/// Uses a sealed record hierarchy so the caller pattern-matches
/// instead of an enum + out-param.
/// </summary>
public abstract record ApiKeyAuthenticationResult
{
    private ApiKeyAuthenticationResult() { }

    /// <summary>The presented key matches its stored hash and is
    /// still active. The principal carries
    /// <c>permission</c> claims for the key's scope plus an
    /// <c>is_service</c> claim so the authorization pipeline knows
    /// this is a machine caller.</summary>
    /// <param name="Principal">Built bearer principal with the key's
    /// resolved permissions.</param>
    /// <param name="KeyId">Echo of the matched key id (used for
    /// audit + last-used-at debouncing).</param>
    public sealed record Success(ClaimsPrincipal Principal, Guid KeyId) : ApiKeyAuthenticationResult;

    /// <summary>Kid is unknown. The framework will surface a 401 with
    /// <c>error="invalid_token"</c>.</summary>
    public sealed record NotFound : ApiKeyAuthenticationResult;

    /// <summary>Kid is known but the secret doesn't match its hash,
    /// or the key has been revoked, or it has expired. The framework
    /// surfaces a 401 with <c>error="invalid_token"</c> and
    /// <c>error_description</c> = reason.</summary>
    /// <param name="Reason"></param>
    public sealed record Invalid(string Reason) : ApiKeyAuthenticationResult;
}
