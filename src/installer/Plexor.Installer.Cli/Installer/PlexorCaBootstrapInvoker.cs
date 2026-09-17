// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCaBootstrapInvoker — thin wrapper around
// Plexor.Shared.Mtls.PlexorCaBootstrap.EnsureCertificates, configured
// for the production install at <data>/mtls/.
//
// The Host composition root also calls PlexorCaBootstrap on startup;
// running it here is belt-and-suspenders so the certs exist BEFORE
// systemctl enable --now (so operators can curl --cacert right after
// init, before the first service start).
//
// PlexorCaBootstrap itself is in Plexor.Shared.Mtls; this file only
// wires the CertAuthorityOptions with the install's paths so the
// bootstrap writes to <data>/mtls/ instead of the dev-certs root.
// ============================================================================

using Microsoft.Extensions.Logging.Abstractions;
using Plexor.Shared.Mtls;

namespace Plexor.Installer.Cli.Installer;

/// <summary>
///     Run <see cref="PlexorCaBootstrap.EnsureCertificates" /> with
///     paths rooted at <see cref="InstallerPaths.DataDirectory" />.
///     Writes the CA root + key to <c>&lt;data&gt;/mtls/</c> and the
///     host server cert + key to <c>&lt;data&gt;/mtls/host</c>.
/// </summary>
public static class PlexorCaBootstrapInvoker
{
    /// <summary>
    ///     Default CA lifetime: 10 years. Long enough that an
    ///     operator-installed cluster doesn't need a renewal in
    ///     v0.1, short enough that the rotation machinery has a
    ///     non-zero schedule to exercise in v0.2.
    /// </summary>
    public static readonly TimeSpan DefaultCaLifetime = TimeSpan.FromDays(3650);

    /// <summary>
    ///     Idempotent: reuses existing cert files if present,
    ///     generates fresh ones on first boot. Returns when the
    ///     CA + host cert files are on disk.
    /// </summary>
    /// <param name="dataDir">
    ///     Plexor data directory. The CA + certs live in a
    ///     <c>mtls/</c> subdirectory.
    /// </param>
    /// <param name="hostSubjectAltNames">
    ///     SAN entries (DNS / IP) for the host server cert.
    ///     Defaults to <c>["localhost", "127.0.0.1"]</c>.
    /// </param>
    public static void EnsureCertificates(
        string dataDir,
        IReadOnlyCollection<string>? hostSubjectAltNames = null)
    {
        var mtlsDir = Path.Combine(dataDir, "mtls");
        var options = new CertAuthorityOptions
        {
            CertPath = Path.Combine(mtlsDir, "ca.crt"),
            KeyPath = Path.Combine(mtlsDir, "ca.key"),
            HostCertPath = Path.Combine(mtlsDir, "host.crt"),
            HostKeyPath = Path.Combine(mtlsDir, "host.key"),
            CaLifetime = DefaultCaLifetime,
        };

        PlexorCaBootstrap.EnsureCertificates(
            options,
            NullLogger.Instance,
            hostSubjectAltNames);
    }
}
