// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCaBootstrap unit tests — the eager, synchronous CA + host-cert
// setup that runs from Program.cs BEFORE ConfigureKestrel. This helper
// is the only thing standing between a fresh install and "Kestrel can't
// find a server cert". Boot-time bugs here block every host startup.
//
// We construct absolute CertAuthorityOptions pointing at a per-test temp
// directory so PlexorCaBootstrap.ResolvePaths leaves them untouched (no
// dev-root dependency in unit tests).
// ============================================================================

using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Plexor.Shared.Mtls.Unit;

/// <summary>
///     Unit tests for <see cref="PlexorCaBootstrap.EnsureCertificates" />
///     — the host composition root's idempotent first-boot helper.
///     Locks down file generation, idempotency, and reuse-on-subsequent-run
///     contracts.
/// </summary>
public sealed class PlexorCaBootstrapShould
{
    /// <summary>
    ///     First-boot on a fresh dir must produce CA + host cert +
    ///     key files, all written as parseable PEM blocks so the
    ///     next boot's X509CertificateLoader can rehydrate them.
    /// </summary>
    [Fact(DisplayName = "Given a fresh temp dir, when EnsureCertificates, then writes CA + host cert + key files")]
    public void FirstRunCreatesRootAndHostCert()
    {
        var tempDir = BootstrapTestHelpers.NewTempDir("plexor-bootstrap-firstrun");
        var options = BootstrapTestHelpers.CreateOptionsInTempDir(tempDir);

        try
        {
            PlexorCaBootstrap.EnsureCertificates(
                options,
                NullLogger.Instance);

            File.Exists(options.CertPath).ShouldBeTrue(
                "CA root cert must be written — Kestrel's mTLS chain needs it.");
            File.Exists(options.KeyPath).ShouldBeTrue(
                "CA root private key must be written — without it no leaf can be issued.");
            File.Exists(options.HostCertPath).ShouldBeTrue(
                "host server cert must be written — Kestrel's UseHttps reads this.");
            File.Exists(options.HostKeyPath).ShouldBeTrue(
                "host server private key must be written — Kestrel's UseHttps needs the matching key.");

            var certPem = File.ReadAllText(options.CertPath);
            certPem.ShouldStartWith("-----BEGIN CERTIFICATE-----");
            certPem.ShouldEndWith("-----END CERTIFICATE-----");
        }
        finally
        {
            BootstrapTestHelpers.CleanupTempDir(tempDir);
        }
    }

    /// <summary>
    ///     Subsequent boots must NOT regenerate any file — the
    ///     existing CA cert + key are byte-identical across runs.
    ///     A regenerated root would orphan every issued leaf and
    ///     a regenerated host cert would invalidate the host's
    ///     Kestrel binding.
    /// </summary>
    [Fact(DisplayName = "Given a populated temp dir, when EnsureCertificates runs again, then files are unchanged byte-for-byte")]
    public void SecondRunDoesNotOverwriteExistingFiles()
    {
        var tempDir = BootstrapTestHelpers.NewTempDir("plexor-bootstrap-idempotent");
        var options = BootstrapTestHelpers.CreateOptionsInTempDir(tempDir);

        try
        {
            PlexorCaBootstrap.EnsureCertificates(
                options,
                NullLogger.Instance);

            var caCertBytesFirst = File.ReadAllBytes(options.CertPath);
            var caKeyBytesFirst = File.ReadAllBytes(options.KeyPath);
            var hostCertBytesFirst = File.ReadAllBytes(options.HostCertPath);
            var hostKeyBytesFirst = File.ReadAllBytes(options.HostKeyPath);

            Thread.Sleep(50);

            PlexorCaBootstrap.EnsureCertificates(
                options,
                NullLogger.Instance);

            File.ReadAllBytes(options.CertPath)
                .ShouldBe(caCertBytesFirst,
                    "CA cert must be reused on subsequent runs — regenerating " +
                    "would invalidate every issued leaf.");
            File.ReadAllBytes(options.KeyPath)
                .ShouldBe(caKeyBytesFirst,
                    "CA private key must be reused — a new key would orphan every leaf.");
            File.ReadAllBytes(options.HostCertPath)
                .ShouldBe(hostCertBytesFirst,
                    "host cert must be reused — Kestrel's UseHttps is bound to this file path.");
            File.ReadAllBytes(options.HostKeyPath)
                .ShouldBe(hostKeyBytesFirst,
                    "host private key must be reused — a new key would leave Kestrel unable to load it.");
        }
        finally
        {
            BootstrapTestHelpers.CleanupTempDir(tempDir);
        }
    }

    /// <summary>
    ///     The loaded root cert's serial must be stable across runs
    ///     — a fresh serial means the CA was regenerated and every
    ///     issued leaf is orphaned.
    /// </summary>
    [Fact(DisplayName = "Given a populated temp dir, when EnsureCertificates runs again, then the loaded root cert's serial matches the first run's")]
    public void ReturnsExistingRootOnSubsequentRuns()
    {
        var tempDir = BootstrapTestHelpers.NewTempDir("plexor-bootstrap-reuse");
        var options = BootstrapTestHelpers.CreateOptionsInTempDir(tempDir);

        try
        {
            PlexorCaBootstrap.EnsureCertificates(
                options,
                NullLogger.Instance);

            var firstRoot = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                .LoadCertificate(File.ReadAllBytes(options.CertPath));
            var firstSerial = firstRoot.SerialNumberBytes;
            firstRoot.Dispose();

            PlexorCaBootstrap.EnsureCertificates(
                options,
                NullLogger.Instance);

            var secondRoot = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                .LoadCertificate(File.ReadAllBytes(options.CertPath));
            var secondSerial = secondRoot.SerialNumberBytes;
            secondRoot.Dispose();

            secondSerial.ShouldBe(firstSerial,
                "root serial must be stable across runs — a fresh serial " +
                "means the CA was regenerated and every issued leaf is orphaned.");
        }
        finally
        {
            BootstrapTestHelpers.CleanupTempDir(tempDir);
        }
    }
}

/// <summary>
///     File-local setup helpers for the PlexorCaBootstrap tests —
///     temp dir creation, options wiring, and best-effort cleanup.
///     File-scoped so it cannot leak to other test files via a
///     shared <c>Helpers/</c> folder.
/// </summary>
file static class BootstrapTestHelpers
{
    /// <summary>
    ///     Returns a unique per-test temp dir path under
    ///     <see cref="Path.GetTempPath" />. The directory is NOT
    ///     created — callers must create it themselves if needed.
    /// </summary>
    public static string NewTempDir(string prefix)
    {
        return Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    ///     Builds a <see cref="CertAuthorityOptions" /> whose paths
    ///     point at <paramref name="tempDir" />. The paths are
    ///     absolute so <c>PlexorCaBootstrap.ResolvePaths</c> leaves
    ///     them untouched (the resolver is private).
    /// </summary>
    public static CertAuthorityOptions CreateOptionsInTempDir(string tempDir)
    {
        return new CertAuthorityOptions
        {
            CertPath = Path.Combine(tempDir, "ca.crt"),
            KeyPath = Path.Combine(tempDir, "ca.key"),
            HostCertPath = Path.Combine(tempDir, "host.pem"),
            HostKeyPath = Path.Combine(tempDir, "host.key"),
        };
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
