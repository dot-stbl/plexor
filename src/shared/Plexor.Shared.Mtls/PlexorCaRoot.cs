// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCaRoot — the Plexor CA root cert, with a thread-safe in-memory
// cache. Lazy-loaded on first access; the PlexorCaStartup
// IHostedService triggers a warm load at startup so a misconfigured
// filesystem fails fast at boot, not on the first NodeJoin.
// ============================================================================

using System.Security.Cryptography.X509Certificates;

namespace Plexor.Shared.Mtls;

/// <summary>
///     Thread-safe holder for the Plexor CA root cert. Generates a
///     fresh root on disk if none exists, then serves it from
///     memory for the lifetime of the process.
/// </summary>
/// <remarks>Construct a root holder backed by the given file store.</remarks>
public sealed class PlexorCaRoot(PlexorCaFileStore fileStore)
{
    private readonly Lock gate = new();
    private X509Certificate2? cert;
    private byte[]? pem;

    /// <summary>The .NET cert wrapper (cert + private key).</summary>
    public X509Certificate2 GetCertificate()
    {
        if (cert is not null)
        {
            return cert;
        }

        lock (gate)
        {
            return cert ??= EnsureLoaded();
        }
    }

    /// <summary>
    ///     CA root cert in PEM bytes — sent to nodes at join time so
    ///     the NodeAgent can verify the host's server cert (mutual
    ///     trust).
    /// </summary>
    public byte[] GetPem()
    {
        if (pem is not null)
        {
            return pem;
        }

        lock (gate)
        {
            return pem ??= System.Text.Encoding.UTF8.GetBytes(
                X509Authority.ToPem(GetCertificate()));
        }
    }

    private X509Certificate2 EnsureLoaded()
    {
        if (fileStore.RootExists())
        {
            return fileStore.ReadRoot();
        }

        var fresh = X509Authority.CreateRoot(
            X509Authority.BuildDn("Plexor Root CA"),
            PlexorCertAuthorityInstaller.DefaultCaLifetime);
        fileStore.WriteRoot(fresh);
        return fresh;
    }
}
