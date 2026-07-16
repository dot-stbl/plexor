// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// X509Authority — Plexor CA cert lifecycle on top of
// System.Security.Cryptography.X509Certificates (.NET 8+).
//
// We considered BouncyCastle for this — it's the canonical X.509
// library in the .NET ecosystem, but the 2.5.x release shipped
// with breaking changes in static field names (X509ObjectIdentifiers
// .BasicConstraints, X509Extension.KeyUsage) and the API surface
// requires a fair amount of fully-qualified dance to avoid
// name collisions with System.Security.Cryptography. The same
// cert lifecycle on built-in .NET 10 is straightforward:
// RSA.Create() + CertificateRequest + X509CertificateLoader +
// PemEncoding. No third-party dependency, no ambiguity.
//
// API surface (all public methods are stateless):
//   CreateRoot     — generate a self-signed CA root
//   IssueLeaf      — sign a leaf cert against an existing CA
//   ToPem          — PEM-encode a cert's public half
//   PrivateKeyToPem — PEM-encode a cert's private key as PKCS#8
//   LoadFromPemFiles — rehydrate (cert + key) from PEM files on disk
// ============================================================================

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Plexor.Shared.Mtls;

/// <summary>
///     Stateless X.509 authority. Methods that produce or consume
///     a private key are instance methods only so they can be
///     resolved through DI; the methods themselves do not carry
///     state across calls.
/// </summary>
public sealed class X509Authority
{
    // OIDs we set on every cert. Numeric form is unambiguous
    // across .NET versions; the static readonly fields on
    // OidCollection are stable but the string overload is what
    // X509Certificate2's extension builder expects.
    private const string BasicConstraintsOid = "2.5.29.19";
    private const string KeyUsageOid = "2.5.29.15";
    private const string ExtendedKeyUsageOid = "2.5.29.37";

    private const string ClientAuthPurposeOid = "1.3.6.1.5.5.7.3.2";
    private const string ServerAuthPurposeOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>
    ///     Generate a self-signed CA root. Returns the cert with
    ///     the private key attached so Kestrel, X509Chain, and our
    ///     middleware can consume it without re-importing.
    /// </summary>
    public static X509Certificate2 CreateRoot(
        X500DistinguishedName subject,
        TimeSpan validity)
    {
        var rsa = RSA.Create(keySizeInBits: 4096);

        var request = new CertificateRequest(
            subject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        AddCaExtensions(request);

        // CreateSelfSigned returns a cert that already carries the
        // private key (the request's key is attached by the BCL on
        // cert creation since .NET 9). Don't CopyWithPrivateKey —
        // it throws "certificate already has an associated private key".
        var now = DateTimeOffset.UtcNow;
        return request.CreateSelfSigned(
            new DateTimeOffset(now.UtcDateTime, TimeSpan.Zero),
            new DateTimeOffset(now.Add(validity).UtcDateTime, TimeSpan.Zero));
    }

    /// <summary>
    ///     Issue a leaf cert signed by <paramref name="caCert" />. The
    ///     CA's private key is read off the cert via
    ///     <see cref="GetRsaPrivateKey" />.
    /// </summary>
    public static X509Certificate2 IssueLeaf(
        X500DistinguishedName subject,
        X509Certificate2 caCert,
        TimeSpan validity,
        LeafKind kind = LeafKind.Client,
        IReadOnlyCollection<string>? subjectAltNames = null)
    {
        using var leafRsa = RSA.Create(keySizeInBits: 2048);
        using var caRsa = GetRsaPrivateKey(caCert)
            ?? throw new InvalidOperationException(
                "CA cert has no RSA private key — was it loaded with the key?");

        var request = new CertificateRequest(
            subject,
            leafRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        AddLeafExtensions(request, kind, subjectAltNames);

        var now = DateTimeOffset.UtcNow;
        var cert = request.Create(
            caCert,    // issuerCert: .NET extracts subject + public key + private key
            new DateTimeOffset(now.UtcDateTime, TimeSpan.Zero),
            new DateTimeOffset(now.Add(validity).UtcDateTime, TimeSpan.Zero),
            NewSerial());

        return cert.CopyWithPrivateKey(leafRsa);
    }

    /// <summary>
    ///     What role a leaf cert plays. Drives the EKU extension:
    ///     client certs get ClientAuth, server certs get ServerAuth.
    /// </summary>
    public enum LeafKind
    {
        /// <summary>Client cert — authenticates the holder to a TLS server (mTLS NodeAgent).</summary>
        Client,
        /// <summary>Server cert — authenticates the holder as a TLS server (Kestrel host).</summary>
        Server,
    }

    /// <summary>PEM-encode a cert's public half (BEGIN CERTIFICATE).</summary>
    public static string ToPem(X509Certificate2 cert)
    {
        return PemEncoding.WriteString(
            "CERTIFICATE",
            cert.Export(X509ContentType.Cert));
    }

    /// <summary>
    ///     PKCS#12 (PFX) bundle of the cert + private key, password-
    ///     protected. Used for Kestrel server-cert loading
    ///     (<c>UseHttps(pfxPath, password)</c>). The PFX contains
    ///     exactly one cert (the leaf) and its matching key.
    /// </summary>
    public static byte[] SaveAsPfx(X509Certificate2 cert, string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException(
                "PFX password must not be empty — Kestrel cannot load a passwordless PFX.",
                nameof(password));
        }

        return cert.Export(X509ContentType.Pfx, password);
    }

    /// <summary>
    ///     PEM-encode a cert's private key as PKCS#8 (BEGIN PRIVATE
    ///     KEY). Used by the NodeAgent join response so the node can
    ///     write it to /var/lib/plexor/node.key.
    /// </summary>
    public static string PrivateKeyToPem(X509Certificate2 cert)
    {
        var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException(
                "Cert has no RSA private key — cannot export PEM.");
        return PemEncoding.WriteString(
            "PRIVATE KEY",
            rsa.ExportPkcs8PrivateKey());
    }

    /// <summary>
    ///     Rehydrate a CA cert from two PEM files on disk. Used at
    ///     host startup to load the persisted root.
    /// </summary>
    public static X509Certificate2 LoadFromPemFiles(string certPath, string keyPath)
    {
        var cert = X509CertificateLoader.LoadCertificate(
            File.ReadAllBytes(certPath));

        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(keyPath));

        return cert.CopyWithPrivateKey(rsa);
    }

    // -- extension builders -------------------------------------------------

    private static void AddCaExtensions(CertificateRequest request)
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

    private static void AddLeafExtensions(
        CertificateRequest request,
        LeafKind kind,
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
            new Oid(kind == LeafKind.Server ? ServerAuthPurposeOid : ClientAuthPurposeOid),
        };
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(eku, critical: false));

        // SAN entries for server certs. Without them, NodeAgent's
        // TLS stack rejects the host's server cert during the
        // chain validation step even though the chain itself builds.
        // DNS / IP entries from config — the plan hard-codes
        // localhost + 127.0.0.1 for dev, OpenNebula LAN for prod.
        if (kind == LeafKind.Server && subjectAltNames is { Count: > 0 })
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            foreach (var name in subjectAltNames)
            {
                if (Uri.CheckHostName(name) == UriHostNameType.Dns)
                {
                    sanBuilder.AddDnsName(name);
                }
                else if (System.Net.IPAddress.TryParse(name, out var ip))
                {
                    sanBuilder.AddIpAddress(ip);
                }
            }

            var san = sanBuilder.Build();
            request.CertificateExtensions.Add(san);
        }
    }

    // -- helpers ------------------------------------------------------------

    /// <summary>
    ///     Random 64-bit serial with the MSB cleared (X.509 requires
    ///     the serial to be a positive integer).
    /// </summary>
    private static byte[] NewSerial()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        bytes[0] &= 0x7F;
        return bytes.ToArray();
    }

    /// <summary>
    ///     Read an RSA private key off a <see cref="X509Certificate2" />.
    ///     Returns null if the cert was loaded without its private
    ///     key (e.g. read from disk without the matching key file).
    /// </summary>
    private static RSA? GetRsaPrivateKey(X509Certificate2 cert)
    {
        return cert.GetRSAPrivateKey();
    }

    /// <summary>
    ///     Build a distinguished name from raw parts (single CN,
    ///     single O, single C). Used by the file-system authority
    ///     to mint the CA subject without string concatenation.
    /// </summary>
    public static X500DistinguishedName BuildDn(
        string cn,
        string organization = "Plexor",
        string country = "US")
    {
        return new X500DistinguishedName(
            $"CN={cn}, O={organization}, C={country}");
    }

    /// <summary>
    ///     Extract the CN (Common Name) component from an X.500
    ///     distinguished name string (<c>X509Certificate2.Subject</c>).
    ///     Returns an empty string if no CN is present. The Plexor
    ///     NodeAgent cert carries its <c>NodeId</c> as the CN
    ///     (<c>node_&lt;26-char-prefixed-ulid&gt;</c>); callers parse it
    ///     via <c>IdParse.ParseNodeId</c>.
    /// </summary>
    public static string ExtractCommonName(string subject)
    {
        foreach (var part in subject.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.Ordinal))
            {
                return trimmed[3..];
            }
        }

        return string.Empty;
    }
}