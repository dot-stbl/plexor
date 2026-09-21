// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereOptions — IOptions-bound configuration for the vSphere provider.
// Bound from the [Providers:VSphere] section of plexor.yaml /
// PLX_PROVIDERS_VSPHERE_* env vars by the composition root
// (Plexor.Host Program.cs).
//
// vSphere auth uses HTTP basic with a service account credential. The
// username is config-driven (non-secret); the password comes from the
// environment via the [Providers:VSphere:Password] key and is never
// persisted to the config file — see secrets.md + the env-only rule in
// configuration-toml-env.md §4.
//
// The IgnoreTlsErrors flag is dev-only; flipping it true in production
// silently downgrades the TLS surface (interceptor log warning only).
//
// v1 opt-in: the operator may deploy Plexor without a vSphere
// integration. The options therefore don't use [Required] — missing
// values produce a 503 from the inventory endpoint ("vSphere not
// configured") instead of failing the host startup. The Url + Length
// attributes still validate the *shape* of any value the operator
// does supply.
// ============================================================================

using System.ComponentModel.DataAnnotations;

namespace Plexor.Providers.VSphere;

/// <summary>
///     Runtime configuration for the vSphere (vCenter REST) provider.
///     Bound by the host composition root via
///     <c>AddOptions&lt;VSphereOptions&gt;().Bind(...).ValidateDataAnnotations()</c>.
///     Fields are not [Required] because vSphere is opt-in — a host
///     can run without vSphere configured. The inventory endpoint
///     surfaces a 503 ProblemDetails when the section is missing or
///     incomplete instead of failing startup.
/// </summary>
public sealed class VSphereOptions
{
    /// <summary>
    ///     Config section name; matches <c>plexor.yaml</c> →
    ///     <c>[Providers.VSphere]</c> or <c>PLX_PROVIDERS_VSPHERE_*</c>
    ///     env vars.
    /// </summary>
    public const string SectionName = "Providers:VSphere";

    /// <summary>
    ///     vCenter base URL including scheme (e.g.
    ///     <c>https://vcenter.example.com/sdk</c>). The provider
    ///     composes relative paths against this root. Optional in
    ///     v1 — see class remarks.
    /// </summary>
    [Url]
    [MaxLength(2048)]
    public string? VCenterUrl { get; init; }

    /// <summary>
    ///     vCenter service-account username (e.g.
    ///     <c>plexor-svc@vsphere.local</c>). Not a secret — Plexor
    ///     operators may rotate the password without touching this
    ///     field.
    /// </summary>
    [MaxLength(128)]
    public string? Username { get; init; }

    /// <summary>
    ///     vCenter service-account password. Env-only: the
    ///     configuration file must NOT contain this value — bind
    ///     from <c>PLX_PROVIDERS_VSPHERE_PASSWORD</c> at deploy time.
    /// </summary>
    [MaxLength(512)]
    public string? Password { get; init; }

    /// <summary>
    ///     Skip TLS certificate validation when talking to vCenter.
    ///     Dev-only escape hatch for self-signed certs in lab /
    ///     staging. <b>Must be false in production.</b>
    /// </summary>
    public bool IgnoreTlsErrors { get; init; }

    /// <summary>
    ///     Returns <c>true</c> when every required field is
    ///     populated — the inventory + provisioning endpoints use
    ///     this to surface 503 ProblemDetails when vSphere isn't
    ///     configured at deploy time.
    /// </summary>
    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(VCenterUrl)
            && !string.IsNullOrWhiteSpace(Username)
            && !string.IsNullOrWhiteSpace(Password);
    }
}
