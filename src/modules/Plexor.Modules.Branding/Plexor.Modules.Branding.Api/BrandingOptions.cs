// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BrandingOptions — IOptions-bound configuration for the branding
// custom-CSS endpoint. The custom CSS path defaults to a path under
// /etc/plexor/ on Linux or %ProgramData%\plexor\ on Windows; the
// operator overrides via the [branding] section of plexor.yaml or
// PLX_BRANDING_CUSTOM_CSS_PATH env var.
// ============================================================================

namespace Plexor.Modules.Branding.Api;

/// <summary>
///     Runtime configuration for the branding capability. v1 ships
///     a single option: the on-disk path to the operator's
///     <c>custom.css</c> file (the "escape hatch" — applied AFTER the
///     runtime theme tokens so the operator can override any token).
/// </summary>
public sealed class BrandingOptions
{
    /// <summary>
    ///     Config section name; matches <c>plexor.yaml</c> →
    ///     <c>[branding]</c> or <c>PLX_BRANDING_*</c> env vars.
    /// </summary>
    public const string SectionName = "Branding";

    /// <summary>
    ///     Path to the operator's <c>custom.css</c> file. Default is
    ///     OS-conventional (Linux: <c>/etc/plexor/custom.css</c>;
    ///     Windows: <c>%ProgramData%\plexor\custom.css</c>). The
    ///     controller returns 404 when the file is missing — the
    ///     operator may not have a custom.css at all (the default
    ///     state in v1).
    /// </summary>
    public string CustomCssPath { get; init; } =
        OperatingSystem.IsWindows()
            ? @"%ProgramData%\plexor\custom.css"
            : "/etc/plexor/custom.css";
}