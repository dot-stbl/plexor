// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RevokedCert — entity record for the forge.revoked_certs table. One
// row per revoked X.509 client cert serial. Inserts cascade from
// cluster / node delete; reads feed the per-request cert verification
// in RevokedCertCache.
// ============================================================================

namespace Plexor.Shared.Mtls.Entities;

/// <summary>
///     Revoked certificate record (one row per serial). The
///     <see cref="Serial" /> is the cert's X.509 serial number,
///     hex-encoded uppercase without colons — the canonical form
///     emitted by <c>X509Certificate2.SerialNumber</c>.
/// </summary>
public sealed class RevokedCert
{
    /// <summary>Cert serial, hex-encoded (uppercase, no colons).</summary>
    public string Serial { get; set; } = string.Empty;

    /// <summary>When the revocation was recorded.</summary>
    public DateTimeOffset RevokedAt { get; set; }

    /// <summary>NodeId of the cluster delete caller (audit trail).</summary>
    public string RevokedBy { get; set; } = string.Empty;

    /// <summary>Optional human-readable reason (e.g. "cluster deleted",
    /// "operator manual revoke").</summary>
    public string Reason { get; set; } = string.Empty;
}