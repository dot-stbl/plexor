using Plexor.Shared.Filtering.Registry;

using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Sigil.Domain.Entities;

/// <summary>
///     OpenSSH public key registered to one user. Used to inject the key
///     into VMs at provision time so the user can SSH in without
///     password auth.
/// </summary>
/// <remarks>
///     <para><b>Fingerprint.</b> SHA-256 hash of the binary public key
///     (after the type prefix and base64 decoding). Unique per tenant
///     (UNIQUE constraint on <c>(tenant_id, fingerprint)</c>); the
///     same key cannot be registered to two users in the same org.</para>
///     <para><b>Lifecycle.</b> <see cref="RevokedAt" /> = <c>null</c>
///     means the key is active. Revoked keys are kept forever for
///     audit trail (no hard delete).</para>
///     <para><b>Validation.</b> The full OpenSSH public key string
///     (e.g. <c>ssh-ed25519 AAAA...</c>) is stored verbatim in
///     <see cref="PublicKey" />; format validation happens in the
///     Application layer when the key is registered (Phase 4).</para>
/// </remarks>
public sealed class SshKey : IFilterableEntity, ICreatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Owner of the key.</summary>
    public Guid UserId { get; init; }

    /// <summary>Tenant the owner belongs to (denormalized for
    /// org-scoped queries + UNIQUE constraint).</summary>
    public Guid OrgId { get; init; }

    /// <summary>Human-readable label (<c>"workstation"</c>,
    /// <c>"github-actions"</c>, ...). Not unique.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>SHA-256 fingerprint of the public key, base64url-encoded
    /// without padding. Globally unique per RFC 4253 (within a tenant).</summary>
    public string Fingerprint { get; init; } = string.Empty;

    /// <summary>Full OpenSSH-format public key string
    /// (e.g. <c>ssh-ed25519 AAAA...</c>).</summary>
    public string PublicKey { get; init; } = string.Empty;

    /// <summary>Last time the key was used to authenticate (UTC), or
    /// <c>null</c> if never.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>When the key was revoked (UTC), or <c>null</c> if active.</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }
}