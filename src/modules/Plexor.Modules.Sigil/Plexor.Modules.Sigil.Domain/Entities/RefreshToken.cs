using Plexor.Shared.Filtering;

using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Sigil.Domain.Entities;

/// <summary>
///     Opaque refresh token (256-bit base64url string) with rotation
///     chain + reuse detection. The raw token is shown to the user on
///     issue and never persisted — only <see cref="TokenHash" />
///     (SHA-256) is stored.
/// </summary>
/// <remarks>
///     <para><b>Single-use.</b> Each refresh request rotates:
///     <c>revoked_at</c> is set on the consumed token, <c>replaced_by</c>
///     points to the new token. See <c>architecture/identity.md</c>
///     §Refresh token rotation + reuse detection for the full chain
///     semantics.</para>
///     <para><b>Family revocation.</b> When a refresh request comes in
///     with a token whose <see cref="RevokedAt" /> is non-null but
///     whose <see cref="FamilyId" /> has another active token, that
///     request is replay-attack evidence — every token in the family
///     is revoked, and the user must re-login. The detection happens
///     in <c>IAuthenticationService.RefreshAsync</c> (Phase 3).</para>
/// </remarks>
public sealed class RefreshToken : IFilterableEntity, ICreatedAt
{
    /// <summary>Unique identifier (UUID v7). Equals the <c>jti</c> claim
    /// in the JWT issued alongside this refresh token.</summary>
    public Guid Id { get; init; }

    /// <summary>User who owns this token.</summary>
    public Guid UserId { get; init; }

    /// <summary>Family id shared by all rotations of the same login
    /// session. Reuse across the family = replay attack → revoke all.</summary>
    public Guid FamilyId { get; init; }

    /// <summary>SHA-256 hash of the raw token, base64url-encoded. Never
    /// the raw token.</summary>
    public string TokenHash { get; init; } = string.Empty;

    /// <summary>When the token expires (UTC). After this time, refresh
    /// requests return 401 and the user must log in again.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>When the token was revoked (UTC), or <c>null</c> if active.
    /// Revoked tokens stay in the table for reuse-detection queries.</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>Next token in the rotation chain. <c>null</c> when this
    /// token is the leaf.</summary>
    public Guid? ReplacedBy { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }
}