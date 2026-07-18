// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// JwtSigningService — IJwtSigningService implementation. ECDSA P-256
// signing via System.IdentityModel.Tokens.Jwt. Reads the active
// SigningKey from ISigningKeyRepository.
// ============================================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     ECDSA P-256 JWT signing service. Reads the active key from
///     <see cref="ISigningKeyRepository" />, signs with the private
///     key, verifies with the public key.
/// </summary>
/// <param name="keys"></param>
/// <param name="revocationChecker"></param>
/// <remarks>
///     <para><b>Why ECDSA P-256.</b> 64-byte signatures, 32-byte
///     public keys — half the size of RSA-2048 with equivalent
///     security. JWS <c>"alg": "ES256"</c> is universally
///     supported (jose, jsonwebtoken, Nimbus JOSE+JWT).</para>
///     <para><b>Private key handling.</b>
///     <see cref="SigningKey.PrivateKeyPem" /> is loaded from the DB
///     (PKCS#8 PEM, plaintext in v0.1). <see cref="ECDsa" /> is
///     <c>using</c>-disposed at the end of each issue / verify call —
///     no instance state. Future Phase 2 swaps to KMS-backed key
///     handles.</para>
///     <para><b>kid in header.</b> Every signed JWT carries the
///     <see cref="SigningKey.Kid" /> in the <c>kid</c> header.
///     Verification reads <c>kid</c> first, then falls back to
///     scanning active keys if the verifier's in-memory cache
///     missed (rotation-window scenario).</para>
/// </remarks>
public sealed class JwtSigningService(
    ISigningKeyRepository keys,
    IUserRevocationChecker revocationChecker) : IJwtSigningService
{
    /// <inheritdoc />
    public Task<IssuedAccessToken> IssueWithLifetimeAsync(
        ClaimsPrincipal principal,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {

        return IssueInternalAsync(principal, lifetime, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IssuedAccessToken> IssueAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        return IssueInternalAsync(principal, lifetime: null, cancellationToken);
    }

    private async Task<IssuedAccessToken> IssueInternalAsync(
        ClaimsPrincipal principal,
        TimeSpan? lifetime,
        CancellationToken cancellationToken)
    {
        var key = await keys.GetActiveAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "No active signing key — bootstrap has not run. " +
                "Ensure SigningKeyBootstrapper is registered as a hosted service.");

        var now = DateTimeOffset.UtcNow;
        var effectiveLifetime = lifetime ?? IJwtSigningService.AccessTokenLifetime;
        var expiresAt = now.Add(effectiveLifetime);

        using var ecdsa = LoadPrivateKey(key.PrivateKeyPem
            ?? throw new InvalidOperationException(
                $"Active signing key '{key.Kid}' has no private key."));

        var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = key.Kid };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        var token = new JwtSecurityToken(
            issuer: IdentityClaims.IssuerValue,
            audience: null,
            claims: principal.Claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        token.Header["kid"] = key.Kid;
        var compact = new JwtSecurityTokenHandler().WriteToken(token);
        return new IssuedAccessToken(compact, expiresAt);
    }

    /// <inheritdoc />
    public async Task<VerifyResult> VerifyAsync(
        string compactJwt,
        CancellationToken cancellationToken = default)
    {
        var handler = new JwtSecurityTokenHandler();
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = IdentityClaims.IssuerValue,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RequireSignedTokens = true,
            RequireExpirationTime = true,
        };

        // Look up the kid from the header so we can scope the
        // signing keys to that one. Falls back to scanning all
        // active keys if the kid is missing/unknown.
        var kid = TryReadKid(compactJwt);
        var key = kid is not null
            ? await keys.GetByKidAsync(kid, cancellationToken)
            : null;

        key ??= await keys.GetActiveAsync(cancellationToken);
        if (key is null)
        {
            return new VerifyResult.Invalid("No active signing key.");
        }

        using var ecdsa = LoadPublicKey(key.PublicKeyPem);
        validation.IssuerSigningKey = new ECDsaSecurityKey(ecdsa) { KeyId = key.Kid };

        try
        {
            var result = await handler.ValidateTokenAsync(
                compactJwt, validation);
            var principal = new ClaimsPrincipal(result.ClaimsIdentity);

            // Post-verify: was this token issued before a password
            // rotation, or for a user that's since been disabled? A
            // signature-valid JWT must NOT be honoured when its
            // subject's auth state changed under it. Skipped for
            // non-JWT identity claims (API keys use a different path).
            var userIdClaim = principal.FindFirstValue(IdentityClaims.UserId);
            if (Guid.TryParse(userIdClaim, out var userId)
                && result.SecurityToken is JwtSecurityToken jwtToken
                && jwtToken.IssuedAt != default)
            {
                var issuedAt = new DateTimeOffset(jwtToken.IssuedAt, TimeSpan.Zero);
                var revocation = await revocationChecker.IsStillValidAsync(
                    userId,
                    issuedAt,
                    cancellationToken);
                switch (revocation)
                {
                    case RevocationCheckResult.Active:
                        break;
                    case RevocationCheckResult.UserDisabled disabled:
                        return new VerifyResult.Invalid(disabled.Reason);
                    case RevocationCheckResult.PasswordRotated rotated:
                        return new VerifyResult.Invalid(
                            $"Password rotated at {rotated.rotatedAt:O}; token predates rotation.");
                }
            }

            return new VerifyResult.Success(principal);
        }
        catch (SecurityTokenMalformedException ex)
        {
            return new VerifyResult.Malformed(ex.Message);
        }
        catch (SecurityTokenException ex)
        {
            return new VerifyResult.Invalid(ex.Message);
        }
    }

    private static string? TryReadKid(string compactJwt)
    {
        try
        {
            var firstDot = compactJwt.IndexOf('.');
            if (firstDot <= 0)
            {
                return null;
            }

            var headerJson = Encoding.UTF8.GetString(
                Base64UrlDecode(compactJwt.AsSpan(0, firstDot)));
            using var doc = System.Text.Json.JsonDocument.Parse(headerJson);
            return doc.RootElement.TryGetProperty("kid", out var kidElement)
                ? kidElement.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(ReadOnlySpan<char> input)
    {
        Span<byte> buffer = stackalloc byte[input.Length];
        if (!Convert.TryFromBase64Chars(input, buffer, out var written))
        {
            throw new FormatException("Invalid base64url segment.");
        }
        return buffer[..written].ToArray();
    }

    private static ECDsa LoadPrivateKey(string pem)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        return ecdsa;
    }

    private static ECDsa LoadPublicKey(string pem)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        return ecdsa;
    }
}
