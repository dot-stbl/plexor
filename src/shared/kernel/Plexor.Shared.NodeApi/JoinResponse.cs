// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// JoinResponse — result of a successful JoinRequest. Extracted from
// NodeContracts.cs (Sprint 3, item 5) per folder-organization.md §1.
// ============================================================================

namespace Plexor.Shared.NodeApi;

/// <summary>
/// <para>
///     Result of a successful <see cref="JoinRequest" />. The
///     NodeAgent persists these and uses them for every
///     subsequent call.
/// </para>
/// <para>
///     The mTLS triple (<see cref="NodeCertificatePem" /> /
///     <see cref="NodePrivateKeyPem" /> /
///     <see cref="CaCertificatePem" />) is issued by the host on
///     every successful join — the NodeAgent writes the cert + key
///     to disk and uses them as the client cert on every subsequent
///     HTTPS call. The CA certificate is included so the NodeAgent
///     can verify the host's server cert (mutual trust at the TLS
///     layer).
/// </para>
/// </summary>
/// <param name="NodeId">Plexor node id (node_&lt;UUIDv7&gt;).</param>
/// <param name="ControlPlaneUrl">Post-join rendezvous point.</param>
/// <param name="NodeCertificatePem">PEM-encoded client cert (CN=node_&lt;id&gt;, signed by Plexor CA).</param>
/// <param name="NodePrivateKeyPem">PKCS#8 PEM-encoded private key for the client cert.</param>
/// <param name="CaCertificatePem">PEM-encoded Plexor CA root.</param>
public sealed record JoinResponse(
    Guid NodeId,
    Uri ControlPlaneUrl,
    string NodeCertificatePem,
    string NodePrivateKeyPem,
    string CaCertificatePem);
