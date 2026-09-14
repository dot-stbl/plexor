// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InMemoryCertificateAuthority — test-only ICertificateAuthority used by
// MtlsAuthMiddleware tests. Mirrors PlexorCertificateIssuer's verifier
// (chain build under an in-memory CA + revocation check + node_
// prefix dispatch) but keeps revocation in a simple HashSet so tests
// don't need a DB.
//
// Use:
//   var ca = X509Authority.CreateRoot(
//       X509Authority.BuildDn("Plexor Test CA"),
//       TimeSpan.FromDays(1));
//   var authority = new InMemoryCertificateAuthority(ca);
//   var cert = authority.IssueClientCert(subject, ttl);
//   authority.Revoke(cert);   // test-only API
//
// Lives in the test project (not Plexor.Shared.Mtls) because it carries
// per-test state — promoting it to shared/ would invite coupling that
// the production code shouldn't pay for.
// ============================================================================

using System.Security.Cryptography.X509Certificates;
using System.Text;
using Plexor.Shared.Mtls;

namespace Plexor.Host.UnitTests.NodeAgent;

/// <summary>
///     In-memory <see cref="ICertificateAuthority" /> for host-side
///     middleware tests. Holds the Plexor CA in memory and a
///     per-instance revoked-serial set; verifies client certs using
///     <see cref="X509Chain" /> with the CA added to
///     <c>ChainPolicy.ExtraStore</c>.
/// </summary>
/// <param name="ca"></param>
internal sealed class InMemoryCertificateAuthority(X509Certificate2 ca) : ICertificateAuthority
{
    private readonly HashSet<string> revokedSerials = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public X509Certificate2 IssueClientCert(X500DistinguishedName subject, TimeSpan ttl)
    {
        return X509Authority.IssueLeaf(subject, ca, ttl, X509Authority.LeafKind.Client);
    }

    /// <inheritdoc />
    public bool VerifyClientCert(X509Certificate2 candidate)
    {
        var chain = new X509Chain();
        chain.ChainPolicy.ExtraStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

        if (!chain.Build(candidate))
        {
            return false;
        }

        // Strict: reject any chain that doesn't end at our Plexor CA.
        // With AllowUnknownCertificateAuthority, X509Chain.Build succeeds
        // for foreign-CA-signed certs (ChainStatus carries PartialChain
        // or UntrustedRoot), so Build() alone is not enough. Confirm
        // the last chain element matches our root.
        //
        // Production PlexorCertificateIssuer currently relies on
        // X509Chain.Build alone — that verifier has a gap: a cert
        // signed by any other CA with a "node_" CN passes. The fix
        // here tracks what the verifier SHOULD do; a follow-up issue
        // is needed to bring production in line.
        if (chain.ChainElements.Count == 0
            || !BytesEqual(chain.ChainElements[^1].Certificate.RawData, ca.RawData))
        {
            return false;
        }

        if (revokedSerials.Contains(candidate.SerialNumber))
        {
            return false;
        }

        var cn = ExtractCn(candidate.SubjectName.Name);
        return cn.StartsWith("node_", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public byte[] GetRootCertificatePem()
    {
        return Encoding.UTF8.GetBytes(X509Authority.ToPem(ca));
    }

    /// <inheritdoc />
    public X509Certificate2 GetRootCertificate()
    {
        return ca;
    }

    /// <summary>
    ///     Mark <paramref name="cert" />'s serial as revoked.
    ///     Subsequent <see cref="VerifyClientCert" /> calls with the
    ///     same cert will return <c>false</c>. Test-only API —
    ///     not part of <see cref="ICertificateAuthority" />.
    /// </summary>
    /// <param name="cert"></param>
    public void Revoke(X509Certificate2 cert)
    {
        revokedSerials.Add(cert.SerialNumber);
    }

    private static string ExtractCn(string distinguishedName)
    {
        foreach (var part in distinguishedName.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.Ordinal))
            {
                return trimmed[3..];
            }
        }

        return string.Empty;
    }

    private static bool BytesEqual(byte[] left, byte[] right)
    {
        return left.AsSpan().SequenceEqual(right);
    }
}
