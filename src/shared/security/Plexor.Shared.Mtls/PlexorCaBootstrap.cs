// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCaBootstrap — synchronous, file-based bootstrap of the Plexor
// CA root and the host's TLS server certificate.
//
// Why eager / static. ConfigureKestrel takes the host PFX path BEFORE
// WebApplicationBuilder.Build() runs — by the time the DI container
// is available, the Kestrel endpoint configuration has already been
// captured. PlexorCaRoot is a lazy singleton that only initializes
// on first GetCertificate() call, which would be too late for the
// server-cert path argument.
//
// So this helper does both jobs up front, synchronously, idempotently:
//   1. Ensure the CA root exists at CertPath / KeyPath
//      (generate if absent, load if present).
//   2. Ensure the host server cert exists at HostCertPath
//      (issue + save as PFX if absent).
// Called from Program.cs BEFORE ConfigureKestrel.
//
// What this helper does NOT do:
//   - It does not touch the DI container — PlexorCaRoot + Issuer
//     are still DI singletons; they read the same files at runtime.
//   - It does not enforce file permissions on the host.pfx —
//     that's beyond MVP (the password does the protecting for now).
//     PlexorCaFileStore handles CA key perms because that's the
//     trust root.
// ============================================================================

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Plexor.Shared.Mtls;

/// <summary>
///     Eager, synchronous bootstrap of CA + host server certificate.
///     Called from the host composition root before ConfigureKestrel.
/// </summary>
public static class PlexorCaBootstrap
{
    /// <summary>
    ///     Idempotent: creates the CA root + host server cert if
    ///     absent, reuses existing files otherwise. Returns once the
    ///     host.pfx is on disk and ready for Kestrel's
    ///     <c>UseHttps(pfxPath, password)</c>.
    /// </summary>
    /// <param name="options">CA + host-cert paths and lifetime.</param>
    /// <param name="logger">For "generated new CA" / "issued host cert" lines.</param>
    /// <param name="hostSubjectAltNames">
    ///     SAN entries for the host server cert (DNS names + IP
    ///     addresses clients can reach it on). Defaults to
    ///     <c>["localhost", "127.0.0.1"]</c> for dev.
    /// </param>
    public static void EnsureCertificates(
        CertAuthorityOptions options,
        ILogger logger,
        IReadOnlyCollection<string>? hostSubjectAltNames = null)
    {
        var ca = EnsureRootCertificate(options, logger);

        EnsureHostServerCertificate(
            ca,
            options,
            logger,
            hostSubjectAltNames ?? ["localhost", "127.0.0.1"]);
    }

    private static X509Certificate2 EnsureRootCertificate(
        CertAuthorityOptions options,
        ILogger logger)
    {
        if (File.Exists(options.CertPath) && File.Exists(options.KeyPath))
        {
            logger.LogInformation(
                "Plexor CA already on disk at {CertPath} — reusing.",
                options.CertPath);

            return X509Authority.LoadFromPemFiles(
                options.CertPath,
                options.KeyPath);
        }

        // First-boot path: generate a fresh root.
        EnsureDirectoryExists(options.CertPath);

        var root = X509Authority.CreateRoot(
            X509Authority.BuildDn("Plexor Root CA"),
            options.CaLifetime);

        WriteRootToDisk(root, options);

        logger.LogInformation(
            "Generated fresh Plexor CA at {CertPath} (validity {Days} days).",
            options.CertPath,
            (int)options.CaLifetime.TotalDays);

        return root;
    }

    private static void WriteRootToDisk(X509Certificate2 root, CertAuthorityOptions options)
    {
        File.WriteAllText(options.CertPath, X509Authority.ToPem(root));
        File.WriteAllText(options.KeyPath, X509Authority.PrivateKeyToPem(root));

        if (!OperatingSystem.IsWindows())
        {
            // The CA private key is the trust root of the entire
            // mTLS plane. It must not be world-readable.
            File.SetUnixFileMode(
                options.CertPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetUnixFileMode(
                options.KeyPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static void EnsureHostServerCertificate(
        X509Certificate2 caCert,
        CertAuthorityOptions options,
        ILogger logger,
        IReadOnlyCollection<string> subjectAltNames)
    {
        if (File.Exists(options.HostCertPath))
        {
            logger.LogInformation(
                "Host server cert already on disk at {PfxPath} — reusing.",
                options.HostCertPath);

            return;
        }

        if (string.IsNullOrEmpty(options.HostCertPassword))
        {
            throw new InvalidOperationException(
                $"CertAuthority:HostCertPassword is required to issue the host server " +
                $"cert at {options.HostCertPath}. Set it in appsettings.json (dev) or " +
                $"the CertAuthority__HostCertPassword env var (prod).");
        }

        EnsureDirectoryExists(options.HostCertPath);

        var hostCert = X509Authority.IssueLeaf(
            X509Authority.BuildDn("plexor-host"),
            caCert,
            options.CaLifetime,
            X509Authority.LeafKind.Server,
            subjectAltNames);

        var pfx = X509Authority.SaveAsPfx(hostCert, options.HostCertPassword);
        File.WriteAllBytes(options.HostCertPath, pfx);

        // Dispose the cert after writing — the file is the source
        // of truth; Kestrel reads it on demand.
        hostCert.Dispose();

        logger.LogInformation(
            "Issued Plexor host server cert at {PfxPath} (SANs: {Sans}).",
            options.HostCertPath,
            string.Join(", ", subjectAltNames));
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}