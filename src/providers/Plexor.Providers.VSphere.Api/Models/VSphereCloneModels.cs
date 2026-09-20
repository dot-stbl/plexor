// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereCloneRequestBody — wire shape for POST /api/v1/vsphere/clone.
// Init-property class (per anti-patterns.md §2 — no positional records
// on wire shapes). The provisioning service maps to the vCenter
// request body internally; the wire shape uses human-friendly names
// (template name, target folder path) and the handler resolves them
// to vCenter mo-refs.
// ============================================================================

namespace Plexor.Providers.VSphere.Api.Models;

/// <summary>
///     Body for <c>POST /api/v1/vsphere/clone</c>. The caller names
///     the source template by display name; the handler resolves the
///     mo-ref by reading the latest cached inventory. Same applies to
///     the target folder path.
/// </summary>
public sealed class VSphereCloneRequestBody
{
    /// <summary>Display name of the source template (e.g.
    /// <c>"ubuntu-22.04-base"</c>). Resolved against the latest
    /// cached inventory; a missing template surfaces as 404.</summary>
    public required string TemplateName { get; init; }

    /// <summary>Display name for the new VM.</summary>
    public required string Name { get; init; }

    /// <summary>Inventory folder path for the new VM (e.g.
    /// <c>"/Datacenter/vm/Tenants/Acme"</c>). Null when the caller
    /// does not care (vCenter default folder).</summary>
    public string? FolderPath { get; init; }
}

/// <summary>
///     Response body for the successful clone. Carries the new VM
///     mo-ref + the audit-trail run id so the caller can poll the
///     provisioning timeline.
/// </summary>
public sealed class VSphereCloneResponse
{
    /// <summary>Audit-trail row id (UUID v7).</summary>
    public Guid RunId { get; init; }

    /// <summary>vCenter mo-ref for the new VM.</summary>
    public required string VmMoref { get; init; }

    /// <summary>Status — always <c>"SUCCESS"</c> on a 200 response.
    /// Failure responses surface as ProblemDetails instead.</summary>
    public string Status { get; init; } = "SUCCESS";
}
