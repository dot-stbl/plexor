// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCaFileStore — filesystem I/O for the Plexor CA root.
// Pure I/O service. No caching, no X.509 logic — just reads and
// writes the two PEM files under /var/lib/plexor/.
// ============================================================================

using System.Security.Cryptography.X509Certificates;

namespace Plexor.Shared.Mtls;

/// <summary>
///     Reads and writes the Plexor CA root cert + private key under
///     the configured paths. PEM format on disk, .NET
///     <see cref="X509Certificate2" /> in memory. Caller is
///     responsible for chmod 0600 on the key file (this class
///     does so on every write).
/// </summary>
/// <remarks>Construct a filesystem store bound to the given options.</remarks>
public sealed class PlexorCaFileStore(CertAuthorityOptions options)
{
    /// <summary>True if both cert and key files exist on disk.</summary>
    public bool RootExists()
    {
        return File.Exists(options.CertPath) && File.Exists(options.KeyPath);
    }

    /// <summary>
    ///     Read the persisted CA root. Rehydrates the .NET cert
    ///     with its private key attached via
    ///     <see cref="X509Authority.LoadFromPemFiles" />.
    /// </summary>
    public X509Certificate2 ReadRoot()
    {
        return X509Authority.LoadFromPemFiles(options.CertPath, options.KeyPath);
    }

    /// <summary>
    ///     Persist the CA root to disk. Creates the parent
    ///     directory if missing and tightens permissions to mode
    ///     0600 on the key file (Unix only — no-op on Windows).
    /// </summary>
    public void WriteRoot(X509Certificate2 root)
    {
        var certDir = Path.GetDirectoryName(options.CertPath);
        if (!string.IsNullOrEmpty(certDir) && !Directory.Exists(certDir))
        {
            Directory.CreateDirectory(certDir);
        }

        File.WriteAllText(options.CertPath, X509Authority.ToPem(root));
        File.WriteAllText(options.KeyPath, X509Authority.PrivateKeyToPem(root));

        if (!OperatingSystem.IsWindows())
        {
            // The CA private key is the trust root of the entire
            // mTLS plane. It must not be world-readable.
            File.SetUnixFileMode(options.CertPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetUnixFileMode(options.KeyPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}