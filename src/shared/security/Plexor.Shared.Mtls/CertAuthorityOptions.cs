// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CertAuthorityOptions — IOptions-bound configuration for the
// FileSystemCertificateAuthority. Values come from
// appsettings.json under the "CertAuthority" section, overridable via
// environment variables (CertAuthority__CertPath etc).
// ============================================================================

namespace Plexor.Shared.Mtls;

/// <summary>
///     Runtime configuration for the filesystem-backed Plexor CA.
///     All paths are absolute; the host refuses to start if it
///     cannot write the directory with mode 0700.
/// </summary>
public sealed class CertAuthorityOptions
{
    /// <summary>
    ///     Config section name; matches <c>appsettings.json</c> →
    ///     <c>"CertAuthority"</c>.
    /// </summary>
    public const string SectionName = "CertAuthority";

    /// <summary>
    ///     Absolute path to the CA root certificate (PEM). Defaults to
    ///     <c>/var/lib/plexor/ca.crt</c> on Linux; on dev machines the
    ///     operator can point at a sibling directory (e.g.
    ///     <c>~/.local/share/plexor/ca.crt</c>).
    /// </summary>
    public string CertPath { get; init; } = "/var/lib/plexor/ca.crt";

    /// <summary>
    ///     Absolute path to the CA root private key (PEM). The file
    ///     is created with mode 0600; the host process account must
    ///     be the only reader.
    /// </summary>
    public string KeyPath { get; init; } = "/var/lib/plexor/ca.key";

    /// <summary>
    ///     Lifetime of the CA root cert (and therefore every leaf cert
    ///     issued under it — see plan §AD-2). MVP uses 10 years.
    /// </summary>
    public TimeSpan CaLifetime { get; init; } = TimeSpan.FromDays(3650);

    /// <summary>
    ///     Path to the host's server certificate (PKCS#12 / PFX).
    ///     Created on first startup if absent.
    /// </summary>
    public string HostCertPath { get; init; } = "/var/lib/plexor/host.pfx";

    /// <summary>
    ///     Password protecting the PKCS#12 server cert bundle. Read
    ///     from <c>CertAuthority:HostCertPassword</c> — production
    ///     deployments inject via env var, never committed to git.
    /// </summary>
    public string HostCertPassword { get; init; } = string.Empty;
}