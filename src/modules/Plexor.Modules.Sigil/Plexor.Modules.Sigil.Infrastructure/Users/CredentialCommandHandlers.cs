// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CredentialCommandHandlers — issue/revoke/list API keys + SSH keys.
// ============================================================================

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Application.Users;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Domain.ValueObjects;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Plexor.Modules.Sigil.Infrastructure.Mappers;
using Plexor.Modules.Sigil.Infrastructure.Persistence;

namespace Plexor.Modules.Sigil.Infrastructure.Users;

/// <summary>
///     Issue a new API key. Validates that the requested permissions
///     are a subset of the owner's effective permissions (security
///     boundary: keys can't exceed what the owner can do).
/// </summary>
/// <param name="db"></param>
/// <param name="permissions"></param>
public sealed class IssueApiKeyCommandHandler(
    IdentityDbContext db,
    IPermissionResolver permissions) : ICommandHandler<IssueApiKeyCommand, IssueApiKeyResult>
{
    /// <inheritdoc />
    public async Task<IssueApiKeyResult> HandleAsync(
        IssueApiKeyCommand command,
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidApiKey,
                "API key name is required.");
        }

        var ownerExists = await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == command.OwnerId && u.OrgId == command.OrgId, cancellationToken);
        if (!ownerExists)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidApiKey,
                "Owner not found in this org.");
        }

        var ownerPermissions = new HashSet<string>(
            await permissions.ResolveAsync(command.OwnerId, command.OrgId, cancellationToken),
            StringComparer.Ordinal);
        var requested = new HashSet<string>(command.Permissions, StringComparer.Ordinal);

        if (!requested.IsSubsetOf(ownerPermissions))
        {
            throw new IdentityException(
                IdentityExceptions.ApiKeyPermissionsExceedOwner,
                "API key permissions exceed the owner's effective permissions.");
        }

        var rawSecret = TokenGenerator.Generate();
        var rawBytes = Encoding.UTF8.GetBytes(rawSecret);
        string secretHash;
        await using (var stream = new MemoryStream(rawBytes, writable: false))
        {
            secretHash = Convert.ToHexString(
                    await SHA256.HashDataAsync(stream, cancellationToken))
                .ToLowerInvariant();
        }

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            OrgId = command.OrgId,
            UserId = command.OwnerId,
            Name = command.Name,
            SecretHash = secretHash,
            Permissions = [.. command.Permissions.Select(static value => new PermissionScope(value))],
            ExpiresAt = command.ExpiresAtUtc,
            LastUsedAt = null,
            RevokedAt = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await db.ApiKeys.AddAsync(apiKey, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new IssueApiKeyResult(apiKey.Id, rawSecret);
    }
}

/// <summary>Revoke an API key. Sets <c>RevokedAt = UtcNow</c>.</summary>
/// <param name="db"></param>
public sealed class RevokeApiKeyCommandHandler(
    IdentityDbContext db) : ICommandHandler<RevokeApiKeyCommand, RevokeApiKeyResult>
{
    /// <inheritdoc />
    public async Task<RevokeApiKeyResult> HandleAsync(
        RevokeApiKeyCommand command,
        CancellationToken cancellationToken = default)
    {

        var rows = await db.ApiKeys
            .Where(key => key.Id == command.KeyId && key.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(key => key.RevokedAt, DateTimeOffset.UtcNow),
                cancellationToken);
        if (rows == 0)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidApiKey,
                "API key not found or already revoked.");
        }

        return new RevokeApiKeyResult(command.KeyId);
    }
}

/// <summary>List API keys for a user.</summary>
/// <param name="db"></param>
/// <param name="mapper">Entity → DTO mapper (Mapperly-generated).</param>
public sealed class ListApiKeysQueryHandler(
    IdentityDbContext db,
    ISigilMapper mapper) : ICommandHandler<ListApiKeysQuery, IReadOnlyCollection<ApiKeySummary>>
{
    /// <inheritdoc />
    public Task<IReadOnlyCollection<ApiKeySummary>> HandleAsync(
        ListApiKeysQuery query,
        CancellationToken cancellationToken = default)
    {
        return db.ApiKeys
            .AsNoTracking()
            .Where(key => key.UserId == query.OwnerId)
            .OrderByDescending(key => key.CreatedAt)
            .Select(key => mapper.ToApiKeySummary(key))
            .ToArrayAsync(cancellationToken)
            .ContinueWith(
                static task => (IReadOnlyCollection<ApiKeySummary>)task.Result,
                cancellationToken,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Current);
    }
}

/// <summary>
///     Register a new SSH key. Validates the public-key string,
///     computes the SHA-256 fingerprint, and persists the row.
/// </summary>
/// <param name="db"></param>
public sealed class AddSshKeyCommandHandler(
    IdentityDbContext db,
    ISigilMapper mapper) : ICommandHandler<AddSshKeyCommand, SshKeySummary>
{
    /// <inheritdoc />
    public async Task<SshKeySummary> HandleAsync(
        AddSshKeyCommand command,
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(command.PublicKey))
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPermission,
                "SSH public key is required.");
        }

        var ownerExists = await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == command.OwnerId && u.OrgId == command.OrgId, cancellationToken);
        if (!ownerExists)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPermission,
                "Owner not found in this org.");
        }

        var fingerprint = ComputeFingerprint(command.PublicKey);
        var conflict = await db.SshKeys
            .AsNoTracking()
            .AnyAsync(key => key.OrgId == command.OrgId
                && key.Fingerprint == fingerprint
                && key.RevokedAt == null,
                cancellationToken);
        if (conflict)
        {
            throw new IdentityException(
                IdentityExceptions.SshKeyFingerprintTaken,
                "SSH key with this fingerprint already exists in this org.");
        }

        var sshKey = new SshKey
        {
            Id = Guid.NewGuid(),
            UserId = command.OwnerId,
            OrgId = command.OrgId,
            Name = command.Name,
            Fingerprint = fingerprint,
            PublicKey = command.PublicKey,
            LastUsedAt = null,
            RevokedAt = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await db.SshKeys.AddAsync(sshKey, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return mapper.ToSshKeySummary(sshKey);
    }

    /// <summary>
    ///     Compute the SHA-256 fingerprint of an OpenSSH public key.
    ///     Real implementation would base64-decode the key body and
    ///     hash the OpenSSH wire-format encoding; v0.1 hashes the
    ///     raw string for fast iteration.
    /// </summary>
    /// <param name="publicKey"></param>
    private static string ComputeFingerprint(string publicKey)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(publicKey)))
            .ToLowerInvariant();
    }
}

/// <summary>Revoke an SSH key.</summary>
/// <param name="db"></param>
public sealed class RevokeSshKeyCommandHandler(
    IdentityDbContext db) : ICommandHandler<RevokeSshKeyCommand, RevokeSshKeyResult>
{
    /// <inheritdoc />
    public async Task<RevokeSshKeyResult> HandleAsync(
        RevokeSshKeyCommand command,
        CancellationToken cancellationToken = default)
    {

        var rows = await db.SshKeys
            .Where(key => key.Id == command.KeyId && key.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(key => key.RevokedAt, DateTimeOffset.UtcNow),
                cancellationToken);
        if (rows == 0)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidPermission,
                "SSH key not found or already revoked.");
        }

        return new RevokeSshKeyResult(command.KeyId);
    }
}

/// <summary>List SSH keys for a user.</summary>
/// <param name="db"></param>
public sealed class ListSshKeysQueryHandler(
    IdentityDbContext db,
    ISigilMapper mapper) : ICommandHandler<ListSshKeysQuery, IReadOnlyCollection<SshKeySummary>>
{
    /// <inheritdoc />
    public Task<IReadOnlyCollection<SshKeySummary>> HandleAsync(
        ListSshKeysQuery query,
        CancellationToken cancellationToken = default)
    {
        return db.SshKeys
            .AsNoTracking()
            .Where(key => key.UserId == query.OwnerId)
            .OrderByDescending(key => key.CreatedAt)
            .Select(key => mapper.ToSshKeySummary(key))
            .ToArrayAsync(cancellationToken)
            .ContinueWith(
                static task => (IReadOnlyCollection<SshKeySummary>)task.Result,
                cancellationToken,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Current);
    }
}

/// <summary>Result of RevokeApiKeyCommand.</summary>
/// <param name="KeyId">The key that was revoked.</param>
public sealed record RevokeApiKeyResult(Guid KeyId);

/// <summary>Result of RevokeSshKeyCommand.</summary>
/// <param name="KeyId">The key that was revoked.</param>
public sealed record RevokeSshKeyResult(Guid KeyId);
