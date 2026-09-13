// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCertificateIssuer unit tests — the cert issuance + verification
// hot path. PlexorCertificateIssuer is the seam handlers and the mTLS
// middleware depend on. Bug here = every node's TLS handshake fails.
//
// We construct the issuer with a real filesystem-backed PlexorCaRoot
// in a per-test temp directory (no DI graph, no DB). The revoked-cert
// cache swallows DB-unreachable errors via the production fail-open
// path, so IsRevoked returns false for unmarked serials even without
// a RevokedCertsDbContext registration.
// ============================================================================

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Plexor.Shared.Mtls.Unit;

/// <summary>
///     Unit tests for <see cref="PlexorCertificateIssuer" /> — the
///     <see cref="ICertificateAuthority" /> implementation that signs
///     and verifies Plexor mTLS certs. Locks down cert chain,
///     CN-prefix defence, foreign-CA rejection, serial uniqueness
///     on re-issue, and validity windows.
/// </summary>
public sealed class PlexorCertificateIssuerShould
{
    /// <summary>
    ///     A freshly-issued node_ cert must verify under the
    ///     issuer's CA: chain builds, revoked cache is empty for
    ///     the new serial, and CN prefix is correct.
    /// </summary>
    [Fact(DisplayName = "Given a valid node_ subject + TTL, when IssueClientCert, then returned cert chains to the Plexor CA")]
    public void IssueClientCertReturnsCertWithChain()
    {
        var (issuer, tempDir) = IssuerTestHelpers.CreateIssuer("chain");

        try
        {
            var subject = X509Authority.BuildDn("node_abc123");

            using var leaf = issuer.IssueClientCert(subject, TimeSpan.FromDays(7));

            X509Authority.ExtractCommonName(leaf.Subject).ShouldBe("node_abc123");

            issuer.VerifyClientCert(leaf).ShouldBeTrue(
                "freshly-issued node_ cert must verify — failure means the " +
                "chain didn't build, the cert is in the revoked set, or the " +
                "CN prefix check rejected our own issuance.");
        }
        finally
        {
            IssuerTestHelpers.CleanupTempDir(tempDir);
        }
    }

    /// <summary>
    ///     Defence in depth: even if a foreign cert somehow chains to
    ///     our CA (via cross-signing or a stolen key), the
    ///     CN-prefix check rejects anything that isn't a Plexor node
    ///     cert.
    /// </summary>
    [Fact(DisplayName = "Given a cert whose CN does not start with node_, when VerifyClientCert, then returns false (CN-prefix defence)")]
    public void VerifyRejectsCertWithNonNodeSubject()
    {
        var (issuer, tempDir) = IssuerTestHelpers.CreateIssuer("non-node");

        try
        {
            var caCert = issuer.GetRootCertificate();
            using var nonNodeLeaf = X509Authority.IssueLeaf(
                X509Authority.BuildDn("plexor-host"),
                caCert,
                TimeSpan.FromDays(7),
                X509Authority.LeafKind.Server);

            issuer.VerifyClientCert(nonNodeLeaf).ShouldBeFalse(
                "non-node_ CN must be rejected — defence in depth against " +
                "any cert that chains to our CA but isn't a Plexor node cert.");
        }
        finally
        {
            IssuerTestHelpers.CleanupTempDir(tempDir);
        }
    }

    /// <summary>
    ///     After the chain-root check landed in #47, a cert signed by
    ///     a foreign CA must fail VerifyClientCert — the chain root
    ///     store only contains our Plexor root, so chain.Build returns
    ///     false and the cert is rejected even though the leaf looks
    ///     structurally valid.
    /// </summary>
    [Fact(DisplayName = "Given a cert signed by a foreign CA, when VerifyClientCert, then returns false (chain root check)")]
    public void VerifyRejectsForeignCaCert()
    {
        var (issuer, tempDir) = IssuerTestHelpers.CreateIssuer("foreign-ca");

        try
        {
            using var foreignRoot = X509Authority.CreateRoot(
                X509Authority.BuildDn("Foreign Root CA"),
                TimeSpan.FromDays(3650));
            using var foreignLeaf = X509Authority.IssueLeaf(
                X509Authority.BuildDn("node_attacker"),
                foreignRoot,
                TimeSpan.FromDays(7),
                X509Authority.LeafKind.Client);

            issuer.VerifyClientCert(foreignLeaf).ShouldBeFalse(
                "cert signed by a foreign CA must be rejected — the chain " +
                "root check (#47) refuses anything that doesn't chain to " +
                "our Plexor CA.");
        }
        finally
        {
            IssuerTestHelpers.CleanupTempDir(tempDir);
        }
    }

    /// <summary>
    ///     Every IssueClientCert call must mint a fresh serial — reusing
    ///     serials would break CRL semantics and let an old (revoked)
    ///     cert look like a new one.
    /// </summary>
    [Fact(DisplayName = "Given two consecutive IssueClientCert calls, when comparing, then a new cert has a different serial")]
    public void ReissueReturnsNewCertWithNewSerial()
    {
        var (issuer, tempDir) = IssuerTestHelpers.CreateIssuer("serial");

        try
        {
            var subject = X509Authority.BuildDn("node_rotate");

            using var first = issuer.IssueClientCert(subject, TimeSpan.FromDays(7));
            using var second = issuer.IssueClientCert(subject, TimeSpan.FromDays(7));

            first.SerialNumberBytes.ShouldNotBe(
                second.SerialNumberBytes,
                "every IssueClientCert call must mint a fresh serial — " +
                "reusing serials would break CRL semantics.");

            issuer.VerifyClientCert(first).ShouldBeTrue();
            issuer.VerifyClientCert(second).ShouldBeTrue();
        }
        finally
        {
            IssuerTestHelpers.CleanupTempDir(tempDir);
        }
    }

    /// <summary>
    ///     NotBefore/NotAfter on an issued client cert must span the
    ///     requested TTL window — drift here silently rotates certs
    ///     early or extends them past the CA's own lifetime.
    /// </summary>
    [Fact(DisplayName = "Given a TTL on IssueClientCert, when cert is read back, then validity window matches the requested TTL")]
    public void IssueClientCertProducesCertInExpectedValidityWindow()
    {
        var (issuer, tempDir) = IssuerTestHelpers.CreateIssuer("clock");

        try
        {
            var ttl = TimeSpan.FromDays(7);
            var before = DateTimeOffset.UtcNow;

            using var leaf = issuer.IssueClientCert(
                X509Authority.BuildDn("node_clock"),
                ttl);

            var after = DateTimeOffset.UtcNow;

            var notBefore = new DateTimeOffset(leaf.NotBefore.ToUniversalTime(), TimeSpan.Zero);
            var notAfter = new DateTimeOffset(leaf.NotAfter.ToUniversalTime(), TimeSpan.Zero);

            notBefore.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(-1));
            notBefore.ShouldBeLessThanOrEqualTo(after.AddSeconds(1));

            notAfter.ShouldBeGreaterThanOrEqualTo(before.Add(ttl).AddSeconds(-1));
            notAfter.ShouldBeLessThanOrEqualTo(after.Add(ttl).AddSeconds(1));
        }
        finally
        {
            IssuerTestHelpers.CleanupTempDir(tempDir);
        }
    }
}

/// <summary>
///     File-local setup helpers for the PlexorCertificateIssuer tests —
///     per-test temp dir, filesystem-backed issuer, and best-effort
///     cleanup. The DB lookup inside RevokedCertCache.RefreshFromDatabase
///     throws on the empty service provider, the catch swallows it,
///     and IsRevoked returns false — the production fail-open path.
/// </summary>
file static class IssuerTestHelpers
{
    /// <summary>
    ///     Builds a <see cref="PlexorCertificateIssuer" /> backed by a
    ///     per-test temp directory. The directory is auto-created on
    ///     first <c>GetCertificate()</c> via PlexorCaRoot.EnsureLoaded,
    ///     which calls <c>fileStore.WriteRoot</c> for first-run
    ///     generation.
    /// </summary>
    /// <param name="purpose">
    ///     Short slug baked into the temp dir name — useful when
    ///     debugging leftover dirs in <c>%TEMP%</c>.
    /// </param>
    public static (PlexorCertificateIssuer issuer, string tempDir) CreateIssuer(string purpose)
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "plexor-mtls-" + purpose + "-" + Guid.NewGuid().ToString("N"));

        var options = new CertAuthorityOptions
        {
            CertPath = Path.Combine(tempDir, "ca.crt"),
            KeyPath = Path.Combine(tempDir, "ca.key"),
            HostCertPath = Path.Combine(tempDir, "host.pem"),
            HostKeyPath = Path.Combine(tempDir, "host.key"),
        };

        var fileStore = new PlexorCaFileStore(options);
        var caRoot = new PlexorCaRoot(fileStore);

        var services = new ServiceCollection().BuildServiceProvider();
        var cacheLogger = NullLogger<RevokedCertCache>.Instance;
        var revokedCache = new RevokedCertCache(
            TimeProvider.System,
            services,
            cacheLogger);

        return (new PlexorCertificateIssuer(caRoot, revokedCache), tempDir);
    }

    /// <summary>
    ///     Best-effort recursive delete of the per-test temp dir.
    ///     Swallows lock-conflict errors — the OS sweeps the temp
    ///     dir eventually, and failing the test on a transient
    ///     antivirus lock would be flaky.
    /// </summary>
    public static void CleanupTempDir(string tempDir)
    {
        if (Directory.Exists(tempDir))
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
