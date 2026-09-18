// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ThemeInstallation — per-tenant record of the community theme the
// operator has activated via the Theme Marketplace (Phase 5+). One
// row per org (UNIQUE on `org_id`); the marketplace controller
// upserts on every activation.
//
// Why a dedicated table rather than reusing `org_theme_config`:
// `OrgThemeConfig` covers built-in preset tokens (preset_id + brand
// overrides) and is the read path for the boot config. The
// marketplace surfaces a separate concept (signed manifests from
// a publisher feed) with its own lifecycle — installing a community
// theme records the HMAC signature of the manifest so the host can
// re-validate the tokens applied by the boot script against the
// publisher identity. Keeping the two rows separate lets the
// boot-config read path stay fast (no manifest join) while the
// marketplace audit trail is independent of the per-tenant theme
// overrides.
//
// Tenant scope is implicit from the `org_id` UNIQUE — at most one
// active marketplace theme per org, just like the existing
// org-theme-config rule.
// ============================================================================

namespace Plexor.Modules.Branding.Domain.Entities;

/// <summary>
///     One row in <c>branding.theme_installations</c> recording the
///     community theme the operator has activated for the tenant.
///     The PK is a per-row UUID (no natural id on the wire — the
///     put endpoint accepts just <c>(theme_id, manifest, signature)</c>);
///     the natural tenant scope is the <c>org_id</c> UNIQUE
///     constraint.
/// </summary>
/// <remarks>
///     <para><b>At most one theme per org.</b> <c>org_id</c> carries
///     a UNIQUE constraint. The PUT endpoint upserts; re-installing
///     a theme mutates the row in place (the row id is preserved
///     across updates so any external observer that captured the id
///     continues to see the same installation record).</para>
///     <para><b>Manifest signature is revalidated on every boot.</b>
///     The HMAC signature is captured at install time so the host
///     can prove the activated manifest bytes came from a holder of
///     the signing key — not just from a tampered FE bundle. The
///     boot script on the FE re-validates the signature against
///     <c>window.__PLEXOR_CONFIG__</c> before applying the tokens;
///     this row is the source of truth for the publisher identity
///     per tenant.</para>
///     <para><b>Delete = reset to operator defaults.</b> The DELETE
///     endpoint removes the row outright (no soft delete). The next
///     call to <c>GET /api/v1/branding/theme</c> returns 404; the
///     boot script falls back to the resolved operator defaults.
///     Audit trail integration with <c>atlas.audit_entries</c>
///     lands in a follow-up phase.</para>
/// </remarks>
public sealed class ThemeInstallation
{
    /// <summary>Unique row identifier (UUID v7, PK).</summary>
    public Guid Id { get; init; }

    /// <summary>FK to <c>realm.organizations.id</c>. UNIQUE — at
    /// most one marketplace installation per org.</summary>
    public Guid OrgId { get; init; }

    /// <summary>
    ///     Stable marketplace id of the installed theme
    ///     (<c>"synthwave-night-dark"</c>, <c>"paper-light"</c>,
    ///     etc.). The host-side community-theme registry
    ///     (<c>CommunityThemeRegistry</c> in
    ///     <c>Plexor.Modules.Branding.Api</c>) maps this to a known
    ///     bundle entry; an unknown id fails the PUT with 404.
    /// </summary>
    public string ThemeId { get; init; } = string.Empty;

    /// <summary>
    ///     Hex-encoded HMAC-SHA256 of the canonical manifest bytes
    ///     (token vocabulary + values + author + version). Verifies
    ///     that the installed manifest was issued by a holder of the
    ///     signing key — the marketplace boot script re-validates
    ///     this against <c>window.__PLEXOR_CONFIG__</c> before
    ///     applying the tokens.
    /// </summary>
    public string ManifestSignature { get; init; } = string.Empty;

    /// <summary>UTC time the activation was applied.</summary>
    public DateTimeOffset ActivatedAt { get; init; }

    /// <summary>Id of the user that applied the activation. Sourced
    /// from <c>ICurrentUser.UserId</c> at write time.</summary>
    public Guid ActivatedBy { get; init; }
}
