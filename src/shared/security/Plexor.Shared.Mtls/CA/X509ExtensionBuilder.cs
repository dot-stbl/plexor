// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// X509ExtensionBuilder — pure helpers that attach the standard
// X.509 v3 extensions (BasicConstraints + KeyUsage + EKU + SAN)
// to a CertificateRequest.
//
// Pulled out of X509Authority per folder-organization.md §1b
// (file decomposition trigger: >300 lines). X509Authority keeps
// the public cert-mint/save/load API; this helper owns the
// extension construction so the authority file reads as a thin
// orchestration layer.
//
// `internal static class` (NOT C# `file static class`) — the
// methods are called from X509Authority.cs in a sibling file.
// File-scope would block that cross-file call.
// ============================================================================

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Plexor.Shared.Mtls;

/// <summary>
///     Pure-function X.509 v3 extension builder. Stateless — each
///     method mutates the <see cref="CertificateRequest" /> it
///     receives and returns nothing.
/// </summary>
internal static class X509ExtensionBuilder
{
    /// <summary>
    ///     OIDs we set on every cert. Numeric form is unambiguous
    ///     across .NET versions; the static readonly fields on
    ///     OidCollection are stable but the string overload is what
    ///     X509Certificate2's extension builder expects.
    /// </summary>
    private const string ClientAuthPurposeOid = "1.3.6.1.5.5.7.3.2";
    private const string ServerAuthPurposeOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>
    ///     Attach CA-specific extensions:
    ///     BasicConstraints(CA=true) + KeyUsage(KeyCertSign | CrlSign).
    /// </summary>
    /// <param name="request"></param>
    public static void AddCaExtensions(CertificateRequest request)
    {
        // BasicConstraints CA=true. pathLengthConstraint=0 is
        // optional and unused (we don't sign sub-CAs in the MVP).
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: true,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        // Key usage: signing + CRL signing.
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                critical: true));
    }

    /// <summary>
    ///     Attach leaf-specific extensions: BasicConstraints(CA=false)
    ///     + KeyUsage(DigitalSignature | KeyEncipherment) + EKU
    ///     (serverAuth or clientAuth, depending on
    ///     <paramref name="kind" />) + SAN entries for server certs.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="kind"></param>
    /// <param name="subjectAltNames"></param>
    public static void AddLeafExtensions(
        CertificateRequest request,
        X509Authority.LeafKind kind,
        IReadOnlyCollection<string>? subjectAltNames)
    {
        // BasicConstraints CA=false. end-entity cert.
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        // Key usage: digital signature + key encipherment for both
        // client and server certs.
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        // EKU depends on kind. The OID is fixed by RFC 5280 — server
        // certs need 1.3.6.1.5.5.7.3.1 (serverAuth), client certs
        // need 1.3.6.1.5.5.7.3.2 (clientAuth). Many stacks reject
        // the cert during TLS handshake if EKU doesn't match.
        var eku = new OidCollection
        {
            new Oid(kind == X509Authority.LeafKind.Server ? ServerAuthPurposeOid : ClientAuthPurposeOid),
        };
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(eku, critical: false));

        // SAN entries for server certs. Without them, NodeAgent's
        // TLS stack rejects the host's server cert during the
        // chain validation step even though the chain itself builds.
        // DNS / IP entries from config — the plan hard-codes
        // localhost + 127.0.0.1 for dev, OpenNebula LAN for prod.
        if (kind == X509Authority.LeafKind.Server && subjectAltNames is { Count: > 0 })
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            foreach (var name in subjectAltNames)
            {
                if (Uri.CheckHostName(name) == UriHostNameType.Dns)
                {
                    sanBuilder.AddDnsName(name);
                }
                else if (IPAddress.TryParse(name, out var ip))
                {
                    sanBuilder.AddIpAddress(ip);
                }
            }

            var san = sanBuilder.Build();
            request.CertificateExtensions.Add(san);
        }
    }
}
