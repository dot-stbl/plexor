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
        var resolved = ResolvePaths(options);
        var ca = EnsureRootCertificate(resolved, logger);

        EnsureHostServerCertificate(
            ca,
            resolved,
            logger,
            hostSubjectAltNames ?? ["localhost", "127.0.0.1"]);
    }

    /// <summary>
    ///     Promotes any relative paths in the options to absolute
    ///     ones joined with <see cref="PlexorPaths.DevRoot" />.
    ///     Absolute paths pass through unchanged so production
    ///     overrides via env var or appsettings.Production.json
    ///     keep working.
    /// </summary>
    /// <param name="src"></param>
    private static CertAuthorityOptions ResolvePaths(CertAuthorityOptions src)
    {
        if (Path.IsPathRooted(src.CertPath) &&
            Path.IsPathRooted(src.KeyPath) &&
            Path.IsPathRooted(src.HostCertPath))
        {
            return src;
        }

        return new CertAuthorityOptions
        {
            CertPath = PlexorPaths.ResolveAgainstDevRoot(src.CertPath),
            KeyPath = PlexorPaths.ResolveAgainstDevRoot(src.KeyPath),
            HostCertPath = PlexorPaths.ResolveAgainstDevRoot(src.HostCertPath),
            HostKeyPath = PlexorPaths.ResolveAgainstDevRoot(src.HostKeyPath),
            CaLifetime = src.CaLifetime,
        };
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
        // PEM format: cert (BEGIN CERTIFICATE block) + private key
        // (BEGIN PRIVATE KEY block) as two sibling files. This is
        // the format every .NET SDK version can load via
        // X509CertificateLoader.LoadCertificate + RSA.ImportFromPem,
        // which is what the Host composition root does after we exit
        // this helper. PFX/PKCS#12 was a dead-end on SDK 10.0.110
        // (X509CertificateLoader.LoadPfxFromFile isn't in this SDK).
        if (File.Exists(options.HostCertPath) && File.Exists(options.HostKeyPath))
        {
            logger.LogInformation(
                "Host server cert + key already on disk at {Path} — reusing.",
                options.HostCertPath);

            return;
        }

        EnsureDirectoryExists(options.HostCertPath);

        var hostCert = X509Authority.IssueLeaf(
            X509Authority.BuildDn("plexor-host"),
            caCert,
            options.CaLifetime,
            X509Authority.LeafKind.Server,
            subjectAltNames);

        // Write the cert + private key as two sibling PEM files.
        // X509Authority.ToPem / PrivateKeyToPem emit BEGIN/END
        // blocks that RSA.ImportFromPem + X509CertificateLoader
        // .LoadCertificate can read back on any .NET version.
        var certPem = X509Authority.ToPem(hostCert);
        var keyPem = X509Authority.PrivateKeyToPem(hostCert);
        File.WriteAllText(options.HostCertPath, certPem);
        File.WriteAllText(options.HostKeyPath, keyPem);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                options.HostCertPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetUnixFileMode(
                options.HostKeyPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        // Dispose the cert after writing — the file is the source
        // of truth; the Host composition root reads it on demand.
        hostCert.Dispose();

        logger.LogInformation(
            "Issued Plexor host server cert at {Path} (SANs: {Sans}).",
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
