// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// API key + SSH key wire shapes. API keys authenticate service-to-service
// callers (NodeAgent ↔ Host, CI bots, etc.); SSH keys gate VM access.
// Both share the "issue → list → revoke" lifecycle.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Users;

/// <summary>
///     Issue a new API key for the owner. The raw secret is generated
///     by the handler — it's returned once in
///     <see cref="IssueApiKeyResult" /> and never persisted.
/// </summary>
/// <param name="OwnerId">User who owns the key.</param>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Name">Human-readable label.</param>
/// <param name="Permissions">Permissions granted to the key. Must be
/// a subset of the owner's effective permissions; otherwise the
/// handler throws
/// <see cref="Domain.Errors.IdentityExceptions.ApiKeyPermissionsExceedOwner" />.</param>
/// <param name="ExpiresAtUtc">Optional expiry (UTC).</param>
public sealed record IssueApiKeyCommand(
    Guid OwnerId,
    Guid OrgId,
    string Name,
    IReadOnlyCollection<string> Permissions,
    DateTimeOffset? ExpiresAtUtc);

/// <summary>Result of IssueApiKeyCommand. The raw secret is shown once.</summary>
/// <param name="KeyId">UUID v7 of the new key (becomes the
/// <c>kid_xxx</c> prefix when the key is used).</param>
/// <param name="RawSecret">Base64url raw secret (43 chars). Show to
/// the caller once, then never re-readable.</param>
public sealed record IssueApiKeyResult(Guid KeyId, string RawSecret);

/// <summary>Revoke an API key.</summary>
/// <param name="KeyId">Target key id.</param>
public sealed record RevokeApiKeyCommand(Guid KeyId);

/// <summary>List API keys for a user (active + revoked).</summary>
/// <param name="OwnerId">Owner whose keys to enumerate.</param>
public sealed record ListApiKeysQuery(Guid OwnerId);

/// <summary>Public projection of <see cref="Domain.Entities.ApiKey" />.
/// Never includes the secret hash.</summary>
/// <summary>Public projection of <see cref="Domain.Entities.ApiKey" />.
/// <c>sealed partial class</c> with init-only properties for
/// Mapperly source-generation compatibility.</summary>
public sealed partial class ApiKeySummary
{
    /// <summary>Key id.</summary>
    public Guid Id { get; init; }

    /// <summary>Owner user.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>Tenant scope.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Human-readable label.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Granted permission strings.</summary>
    public IReadOnlyCollection<string> Permissions { get; init; } = [];

    /// <summary>Optional expiry.</summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }

    /// <summary>Last successful auth time.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>When the key was revoked (null = active).</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
///     Register a new SSH key for a user. The public key is stored
///     verbatim; the private key never leaves the user's machine.
/// </summary>
/// <param name="OwnerId">User the key belongs to.</param>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Name">Human-readable label (<c>"work-laptop"</c>,
/// <c>"ci-build"</c>, ...).</param>
/// <param name="PublicKey">OpenSSH public-key string (the part after
/// <c>ssh-rsa</c>/<c>ssh-ed25519</c>, including the algorithm prefix).</param>
public sealed record AddSshKeyCommand(
    Guid OwnerId,
    Guid OrgId,
    string Name,
    string PublicKey);

/// <summary>Revoke an SSH key.</summary>
/// <param name="KeyId">Target key id.</param>
public sealed record RevokeSshKeyCommand(Guid KeyId);

/// <summary>List SSH keys for a user (active + revoked).</summary>
/// <param name="OwnerId">Owner whose keys to enumerate.</param>
public sealed record ListSshKeysQuery(Guid OwnerId);

/// <summary>Public projection of <see cref="Domain.Entities.SshKey" />.
/// Public-key fingerprint (SHA-256) is exposed for UI display; the
/// raw key material is never returned.
/// <c>sealed partial class</c> with init-only properties.</summary>
public sealed partial class SshKeySummary
{
    /// <summary>Key id.</summary>
    public Guid Id { get; init; }

    /// <summary>Owner user.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>Tenant scope.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Human-readable label.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>SHA-256 fingerprint of the public key (base64, no padding,
    /// no colons — OpenSSH host-key style).</summary>
    public string Fingerprint { get; init; } = string.Empty;

    /// <summary>Last successful VM access time.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>When the key was revoked (null = active).</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
