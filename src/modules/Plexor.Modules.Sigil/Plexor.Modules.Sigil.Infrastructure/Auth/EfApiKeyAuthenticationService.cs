// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfApiKeyAuthenticationService — IApiKeyAuthenticationService backed
// by IdentityDbContext. SHA-256 hashes the presented secret, looks up
// the row by key id, and constant-time compares against the stored
// hash. Returns a ClaimsPrincipal with is_service=true for the
// authorization pipeline.
// ==========================================================================

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     EF Core implementation of <see cref="IApiKeyAuthenticationService" />.
///     Single roundtrip reads the key row (id, secret_hash, permissions,
///     expiry, revoked_at). Constant-time hash comparison via
///     <c>FixedTimeEquals</c> prevents timing leaks on the secret.
/// </summary>
public sealed class EfApiKeyAuthenticationService(
    IdentityDbContext db) : IApiKeyAuthenticationService
{
    /// <inheritdoc />
    public async Task<ApiKeyAuthenticationResult> AuthenticateAsync(
        Guid keyId,
        string rawSecret,
        CancellationToken cancellationToken = default)
    {

        var key = await db.ApiKeys
            .AsNoTracking()
            .Where(k => k.Id == keyId)
            .Select(k => new ApiKeySnapshot(
                k.Id,
                k.OrgId,
                k.UserId,
                k.SecretHash,
                k.Permissions.Select(static p => p.Value).ToArray(),
                k.ExpiresAt,
                k.RevokedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (key is null)
        {
            return new ApiKeyAuthenticationResult.NotFound();
        }

        if (key.RevokedAt is not null)
        {
            return new ApiKeyAuthenticationResult.Invalid("API key revoked.");
        }

        if (key.ExpiresAt is { } expires && expires < DateTimeOffset.UtcNow)
        {
            return new ApiKeyAuthenticationResult.Invalid("API key expired.");
        }

        // Compute the SHA-256 of the presented secret and constant-
        // time compare against the stored hash. Hex-formatted to
        // match the format written by the issue handler
        // (CredentialCommandHandlers.IssueApiKey).
        byte[] presentedHash;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(rawSecret), writable: false))
        {
            presentedHash = await SHA256.HashDataAsync(stream, cancellationToken);
        }
        var storedHashBytes = Convert.FromHexString(key.SecretHash);

        if (!CryptographicOperations.FixedTimeEquals(presentedHash, storedHashBytes))
        {
            return new ApiKeyAuthenticationResult.Invalid("API key secret mismatch.");
        }

        return new ApiKeyAuthenticationResult.Success(
            BuildPrincipal(key),
            key.Id);
    }

    /// <summary>
    ///     Build the canonical <see cref="ClaimsPrincipal" /> for a
    /// matched API key. Permissions are taken from the key's stored
    /// scope (a subset of the owner's effective permissions —
    /// the issuance handler enforces this invariant). The
    /// <c>is_service</c> flag tells downstream endpoints this is a
    /// machine caller (not a human user).
    /// </summary>
    private static ClaimsPrincipal BuildPrincipal(ApiKeySnapshot key)
    {
        var claims = new List<Claim>
        {
            new(IdentityClaims.UserId, key.UserId.ToString()),
            new(IdentityClaims.TenantId, key.OrgId.ToString()),
            new(IdentityClaims.IsService, "true"),
        };

        foreach (var permission in key.Permissions)
        {
            claims.Add(new Claim(IdentityClaims.Permission, permission));
        }

        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticationType: "PlexorApiKey"));
    }

    /// <summary>Internal projection — keeps the EF query to the
    /// columns the handler actually needs.</summary>
    private sealed record ApiKeySnapshot(
        Guid Id,
        Guid OrgId,
        Guid UserId,
        string SecretHash,
        string[] Permissions,
        DateTimeOffset? ExpiresAt,
        DateTimeOffset? RevokedAt);
}
