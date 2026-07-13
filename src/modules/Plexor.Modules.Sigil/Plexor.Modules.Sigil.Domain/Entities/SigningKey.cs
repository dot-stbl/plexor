namespace Plexor.Modules.Sigil.Domain.Entities;

/// <summary>
///     RSA keypair used to sign JWTs. Generated on first start; rotated
///     every 90 days. The verifier caches the most recent two public
///     keys in-memory and falls back to the DB only when the JWT's
///     <c>kid</c> header is unknown (rare — covers rotation windows).
/// </summary>
/// <remarks>
///     <para><b>Single-writer pattern.</b> In v0.1, every host instance
///     generates its own signing keypair on first start if the
///     <c>signing_keys</c> table is empty. Multi-region / multi-cluster
///     deployments (Phase 2) will need a shared key store (e.g. an
///     external KMS or a designated signing-only instance) — out of
///     scope for v0.1.</para>
///     <para><b>Rotation.</b> When <see cref="NotAfter" /> is non-null,
///     the key is no longer used to sign new tokens but is still
///     accepted for verification until the access-JWT lifetime
///     (15 min) elapses. The verifier keeps a sliding window of
///     "active for verify" keys.</para>
///     <para><b>Private key storage.</b> <see cref="PrivateKeyPem" />
///     is stored as PKCS#8 PEM. In v0.1 this column is plaintext
///     (acceptable for self-hosted single-cluster); Phase 2 swaps to
///     KMS-backed envelope encryption.</para>
/// </remarks>
public sealed class SigningKey
{
    /// <summary>Key id, included in the JWT <c>kid</c> header.
    /// Format: <c>key_YYYY_Qn</c> (e.g. <c>key_2025_q4</c>).</summary>
    public string Kid { get; init; } = string.Empty;

    /// <summary>Signing algorithm. v0.1 is always <c>"RS256"</c>.</summary>
    public string Algorithm { get; init; } = "RS256";

    /// <summary>PKCS#8 PEM-encoded public key (SubjectPublicKeyInfo).
    /// Verifier caches this in-memory.</summary>
    public string PublicKeyPem { get; init; } = string.Empty;

    /// <summary>PKCS#8 PEM-encoded private key. Set on the active signing
    /// key only; older rotated keys set this to <c>null</c> after their
    /// private material is removed from the process.</summary>
    public string? PrivateKeyPem { get; init; }

    /// <summary>When the keypair was generated (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the key stopped signing new tokens (UTC), or
    /// <c>null</c> = still the active signer.</summary>
    public DateTimeOffset? NotAfter { get; init; }
}
