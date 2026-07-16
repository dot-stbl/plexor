// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InMemoryCertificateAuthority — test double for the ICertificateAuthority
// used by NodeJoinCommandHandler. We don't want tests to touch the
// filesystem-backed CA (the production stack creates files on the
// host machine) and we don't want each test to re-derive the
// trust chain. The double generates a fresh self-signed CA + signs
// each issued client cert under it, all in memory.
// ============================================================================

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Plexor.Shared.Mtls;

namespace Plexor.Modules.Clusters.Unit.Clusters;

/// <summary>
///     In-memory <see cref="ICertificateAuthority" /> for cluster
///     join-flow tests. Generates a fresh self-signed root on
///     first call to <c>IssueClientCert</c> (one root per test
///     instance is fine — the join flow never validates against
///     the root, only hands the PEM back to the NodeAgent).
/// </summary>
internal sealed class InMemoryCertificateAuthority : ICertificateAuthority, IDisposable
{
    /// <summary>Lock that guards the lazy CA initialization.</summary>
    private readonly System.Threading.Lock gate = new();

    /// <summary>Lazily-created self-signed root. Disposed at end of test.</summary>
    private X509Certificate2? caCert;

    public X509Certificate2 IssueClientCert(X500DistinguishedName subject, TimeSpan ttl)
    {
        var ca = GetOrCreateRoot();
        return X509Authority.IssueLeaf(subject, ca, ttl);
    }

    public X509Certificate2 GetRootCertificate()
    {
        return GetOrCreateRoot();
    }

    public byte[] GetRootCertificatePem()
    {
        return System.Text.Encoding.UTF8.GetBytes(X509Authority.ToPem(GetOrCreateRoot()));
    }

    public bool VerifyClientCert(X509Certificate2 candidate)
    {
        // Tests don't verify the chain — they only assert on
        // the join response shape. Production verifies via
        // PlexorCertificateIssuer which uses the real chain.
        return true;
    }

    private X509Certificate2 GetOrCreateRoot()
    {
        if (caCert is not null)
        {
            return caCert;
        }

        lock (gate)
        {
            if (caCert is not null)
            {
                return caCert;
            }

            using var rsa = RSA.Create(keySizeInBits: 2048);
            var request = new CertificateRequest(
                X509Authority.BuildDn("Plexor Test CA"),
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    certificateAuthority: true,
                    hasPathLengthConstraint: false,
                    pathLengthConstraint: 0,
                    critical: true));

            var now = DateTimeOffset.UtcNow;
            // Leaf TTL in the production path is PlexorCertAuthorityInstaller
            // .DefaultCaLifetime = 10 years — so the test root has to
            // outlive that. 100 years is fine for a test fixture.
            caCert = request.CreateSelfSigned(
                new DateTimeOffset(now.UtcDateTime, TimeSpan.Zero),
                new DateTimeOffset(now.AddYears(100).UtcDateTime, TimeSpan.Zero));
            return caCert;
        }
    }

    public void Dispose()
    {
        caCert?.Dispose();
    }
}