// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ThemeInstallationResponse — wire shape for the GET /
// PUT /api/v1/branding/theme response. Initialised-class shape
// matching the rest of the Branding API surface.
// ============================================================================

namespace Plexor.Modules.Branding.Api.Models.Responses;

/// <summary>
///     Wire shape for the marketplace installation row. Returned
///     by <c>GET /api/v1/branding/theme</c> (200) and
///     <c>PUT /api/v1/branding/theme</c> (200). 404 when no row
///     has been activated yet — the controller maps that to a
///     separate ProblemDetails shape.
/// </summary>
public sealed class ThemeInstallationResponse
{
    /// <summary>Tenant scope — the org whose installation this
    /// row records.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Stable marketplace id of the installed theme.</summary>
    public string ThemeId { get; init; } = string.Empty;

    /// <summary>Publisher-friendly display name
    /// (<c>"Synthwave Night — Dark"</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Publisher-supplied SemVer string.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Publisher handle or org name
    /// (<c>"plexor-themes"</c>).</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Hex-encoded HMAC-SHA256 the publisher attached
    /// to the canonical manifest bytes at install time. The FE
    /// boot script can re-verify the same signature against
    /// <c>window.__PLEXOR_CONFIG__</c> on every reload.</summary>
    public string ManifestSignature { get; init; } = string.Empty;

    /// <summary>UTC time the activation was applied.</summary>
    public DateTimeOffset ActivatedAt { get; init; }

    /// <summary>Id of the user that applied the activation.</summary>
    public Guid ActivatedBy { get; init; }
}
