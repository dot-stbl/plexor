// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ICertificateAuthority — single seam for mTLS cert issuance, verification,
// and root-cert publication. The MVP impl is
// FileSystemCertificateAuthority (CA key in /var/lib/plexor/ca.key,
// mode 0600); Phase 2 swaps to VaultCertificateAuthority via DI
// binding change — no handler code is affected.
// ============================================================================

using System.Security.Cryptography.X509Certificates;

namespace Plexor.Shared.Mtls;

/// <summary>
/// <para>
///     Contract for the Plexor internal CA. The CA signs:
///   - the host's server cert (one-shot at first startup)
///   - each NodeAgent's client cert (per join)
/// </para>
/// <para>
/// All certs share the same CN-prefix convention as Plexor entity IDs
/// (<c>CN=node_&lt;UUIDv7&gt;</c>, <c>CN=plexor-host</c>) so a single
/// chain verifier (<see cref="VerifyClientCert" />) can dispatch on
/// CN prefix when needed.
/// </para>
/// </summary>
public interface ICertificateAuthority
{
    /// <summary>
    ///     Issue a new client cert signed by the Plexor CA.
    ///     Subject CN must be the wire-format NodeId (<c>node_&lt;UUIDv7&gt;</c>).
    /// </summary>
    /// <param name="subject">X500DistinguishedName whose CN is the
    /// canonical Plexor NodeId.</param>
    /// <param name="ttl">How long the cert is valid. MVP uses the
    /// CA lifetime (10 years) — see plan §AD-2.</param>
    /// <returns>The freshly-issued cert with both public + private
    /// keys populated.</returns>
    public X509Certificate2 IssueClientCert(X500DistinguishedName subject, TimeSpan ttl);

    /// <summary>
    ///     Validate a presented client cert against the Plexor CA chain
    ///     and the <c>forge.revoked_certs</c> table. Revocation lookup
    ///     uses an in-memory cache (5s TTL) so the per-request SELECT
    ///     does not bottleneck.
    /// </summary>
    /// <param name="candidate">The cert the NodeAgent presented on
    /// the TLS handshake. The caller passes
    /// <c>HttpContext.Connection.ClientCertificate</c>.</param>
    /// <returns>true iff the cert chain builds under our CA AND the
    /// cert's serial is not in the revoked set.</returns>
    public bool VerifyClientCert(X509Certificate2 candidate);

    /// <summary>
    ///     CA root cert in PEM bytes — sent to nodes at join time so
    ///     the NodeAgent can verify the host's server cert (mutual
    ///     trust).
    /// </summary>
    public byte[] GetRootCertificatePem();

    /// <summary>
    ///     CA root cert as an <see cref="X509Certificate2" />, for the
    ///     Kestrel <c>X509Chain.ExtraStore</c> setup. Single-load,
    ///     cached for the lifetime of the host process.
    /// </summary>
    public X509Certificate2 GetRootCertificate();
}
