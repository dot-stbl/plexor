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
    ///     Path to the CA root certificate (PEM). Default is a
    ///     relative path under <c>dev-certs/</c> at the repository
    ///     root — PlexorCaBootstrap.ResolvePaths rewrites this to
    ///     an absolute path at first-boot. On production deploys
    ///     the operator supplies an absolute path via env var
    ///     (CertAuthority__CertPath) or
    ///     appsettings.Production.json — PlexorCaBootstrap detects
    ///     the change and skips the rewrite.
    /// </summary>
    public string CertPath { get; init; } = "dev-certs/ca.crt";

    /// <summary>
    ///     Path to the CA root private key (PEM). The file is
    ///     created with mode 0600 on Unix; the host process
    ///     account must be the only reader.
    /// </summary>
    public string KeyPath { get; init; } = "dev-certs/ca.key";

    /// <summary>
    ///     Lifetime of the CA root cert (and therefore every leaf
    ///     cert issued under it — see plan §AD-2). MVP uses 10
    ///     years.
    /// </summary>
    public TimeSpan CaLifetime { get; init; } = TimeSpan.FromDays(3650);

    /// <summary>
    /// <para>
    ///     Path to the host's server certificate (PEM). Default
    ///     lives next to the CA in <c>dev-certs/</c>; production
    ///     overrides with the convention from the plan (e.g.
    ///     <c>/var/lib/plexor/host.pem</c>).
    /// </para>
    /// <para>
    ///     PEM over PKCS#12 because PEM is supported by every
    ///     .NET SDK version via X509CertificateLoader.LoadCertificate
    ///     + RSA.ImportFromPem — no deprecated ctor needed.
    ///     PEM is also what the NodeAgent uses for the node
    ///     client cert, so the host and agent on-disk formats
    ///     stay symmetric and easy to inspect with openssl.
    /// </para>
    /// </summary>
    public string HostCertPath { get; init; } = "dev-certs/host.pem";

    /// <summary>
    ///     Path to the host's server certificate private key
    ///     (PEM, PKCS#8). Pair with <see cref="HostCertPath" />.
    ///     Production overrides with the convention from the plan
    ///     (e.g. <c>/var/lib/plexor/host.key</c>).
    /// </summary>
    public string HostKeyPath { get; init; } = "dev-certs/host.key";
}
