// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// X509Authority unit tests — the cryptographic primitives that the
// entire mTLS plane depends on. A bug in BuildDn, ToPem, ExtractCommonName,
// IssueLeaf, or SAN generation compromises the CA chain at every node.
//
// We exercise the real .NET X509 stack end-to-end (RSA key generation,
// CertificateRequest, X509Chain.Build, X509CertificateLoader), no mocks.
// Each test creates its own CA + leaf so failures are isolated and
// parallel-safe.
// ============================================================================

using System.Net;
using System.Security.Cryptography.X509Certificates;
using Shouldly;

namespace Plexor.Shared.Mtls.Unit;

/// <summary>
///     Unit tests for <see cref="X509Authority" /> — the certificate
///     building blocks. Locked-down behaviour: DN composition, PEM
///     roundtrip, CN extraction, leaf issuance, SAN generation,
///     and validity dates.
/// </summary>
public sealed class X509AuthorityShould
{
    /// <summary>
    ///     The DN composition must round-trip through
    ///     <see cref="X500DistinguishedName" /> as the canonical
    ///     "CN=..., O=..., C=..." string — that's what every
    ///     downstream consumer (chain builder, SAN extractor) parses.
    /// </summary>
    [Fact(DisplayName = "Given cn + org + country, when BuildDn, then produces CN=…, O=…, C=… distinguished name")]
    public void BuildDnProducesValidDistinguishedName()
    {
        var dn = X509Authority.BuildDn("plexor-host", "Plexor", "US");

        dn.Name.ShouldBe("CN=plexor-host, O=Plexor, C=US");
    }

    /// <summary>
    ///     PEM roundtrip via the BCL loader is the contract every host
    ///     relies on at startup: ToPem emits a single BEGIN CERTIFICATE
    ///     block; X509CertificateLoader.LoadCertificate rehydrates it
    ///     with the subject intact.
    /// </summary>
    [Fact(DisplayName = "Given a generated cert, when ToPem then LoadCertificate, then subject roundtrips")]
    public void ToPemRoundtripsThroughX509CertificateLoader()
    {
        using var root = X509Authority.CreateRoot(
            X509Authority.BuildDn("Plexor Test CA"),
            TimeSpan.FromDays(365));

        var pem = X509Authority.ToPem(root);

        pem.ShouldStartWith("-----BEGIN CERTIFICATE-----");
        pem.ShouldEndWith("-----END CERTIFICATE-----");

        var loaded = X509CertificateLoader.LoadCertificate(
            System.Text.Encoding.UTF8.GetBytes(pem));

        X509Authority.ExtractCommonName(loaded.Subject).ShouldBe("Plexor Test CA");
    }

    /// <summary>
    ///     ExtractCommonName is the parser every NodeAgent cert
    ///     consumer relies on to recover the Plexor NodeId from the
    ///     cert's subject — a CN drift here is a silent auth bug.
    /// </summary>
    [Fact(DisplayName = "Given a DN string, when ExtractCommonName, then returns the CN segment")]
    public void ExtractCommonNameReturnsTheCnSegment()
    {
        const string subject = "CN=plexor-host, O=Plexor, C=US";

        X509Authority.ExtractCommonName(subject).ShouldBe("plexor-host");
    }

    /// <summary>
    ///     IssueLeaf must sign against the provided CA — a signing-key
    ///     bug here would reject every NodeAgent cert at the TLS
    ///     handshake (chain.Build returns false).
    /// </summary>
    [Fact(DisplayName = "Given a CA + subject + TTL, when IssueLeaf, then leaf is signed by CA and carries expected subject")]
    public void IssueLeafProducesCertWithExpectedSubjectAndIssuer()
    {
        using var root = X509Authority.CreateRoot(
            X509Authority.BuildDn("Plexor Root CA"),
            TimeSpan.FromDays(3650));

        var leafSubject = X509Authority.BuildDn("node_test");
        using var leaf = X509Authority.IssueLeaf(
            leafSubject,
            root,
            TimeSpan.FromDays(30),
            X509Authority.LeafKind.Client);

        X509Authority.ExtractCommonName(leaf.Subject).ShouldBe("node_test");

        using var chain = new X509Chain();
        chain.ChainPolicy.ExtraStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags =
            X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.Build(leaf).ShouldBeTrue(
            "leaf must chain to the Plexor CA — a signing bug would " +
            "reject every NodeAgent cert at the TLS handshake.");
    }

    /// <summary>
    ///     Server certs must carry a SAN extension with both DNS and IP
    ///     entries — without it, NodeAgent's TLS stack rejects the host's
    ///     server cert during hostname validation even though the chain
    ///     builds.
    /// </summary>
    [Fact(DisplayName = "Given a server leaf with DNS + IP SANs, when issued, then SAN extension contains both entries")]
    public void IssueLeafGeneratesSanEntriesForHostnameAndIp()
    {
        using var root = X509Authority.CreateRoot(
            X509Authority.BuildDn("Plexor Root CA"),
            TimeSpan.FromDays(3650));

        var sans = new[] { "plexor.example.com", "10.0.0.1" };

        using var leaf = X509Authority.IssueLeaf(
            X509Authority.BuildDn("plexor-host"),
            root,
            TimeSpan.FromDays(30),
            X509Authority.LeafKind.Server,
            sans);

        var sanExtension = leaf.Extensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .SingleOrDefault();
        sanExtension.ShouldNotBeNull(
            "server cert must carry a SAN extension — without it the TLS " +
            "handshake fails hostname validation.");

        var dnsNames = sanExtension.EnumerateDnsNames().ToList();
        dnsNames.ShouldContain("plexor.example.com");

        var ipAddresses = sanExtension.EnumerateIPAddresses().ToList();
        ipAddresses.ShouldContain(IPAddress.Parse("10.0.0.1"));
    }

    /// <summary>
    ///     NotBefore/NotAfter must span the requested TTL window —
    ///     NotBefore is "now" (within 1s drift) and NotAfter is
    ///     "now + TTL" (same drift budget). A drift here silently
    ///     rotates certs too early or extends them past the CA's
    ///     own lifetime.
    /// </summary>
    [Fact(DisplayName = "Given a TTL, when IssueLeaf, then NotBefore/NotAfter span the requested window")]
    public void IssueLeafProducesCertInExpectedValidityWindow()
    {
        using var root = X509Authority.CreateRoot(
            X509Authority.BuildDn("Plexor Root CA"),
            TimeSpan.FromDays(3650));

        var ttl = TimeSpan.FromDays(30);
        var before = DateTimeOffset.UtcNow;

        using var leaf = X509Authority.IssueLeaf(
            X509Authority.BuildDn("node_clock_test"),
            root,
            ttl,
            X509Authority.LeafKind.Client);

        var after = DateTimeOffset.UtcNow;

        var notBefore = new DateTimeOffset(leaf.NotBefore.ToUniversalTime(), TimeSpan.Zero);
        var notAfter = new DateTimeOffset(leaf.NotAfter.ToUniversalTime(), TimeSpan.Zero);

        notBefore.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(-1));
        notBefore.ShouldBeLessThanOrEqualTo(after.AddSeconds(1));

        notAfter.ShouldBeGreaterThanOrEqualTo(before.Add(ttl).AddSeconds(-1));
        notAfter.ShouldBeLessThanOrEqualTo(after.Add(ttl).AddSeconds(1));
    }
}
