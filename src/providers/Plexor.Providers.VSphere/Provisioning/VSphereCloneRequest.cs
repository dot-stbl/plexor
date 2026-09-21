// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereCloneRequest — body shape for POST /api/vcenter/vm?action=clone.
//
// Maps directly to the vCenter documented request body:
//   - source      : template mo-ref to clone from
//   - name        : display name for the new VM
//   - folder      : target VM-folder mo-ref (resolved by the
//                   provisioning service from the tenant Folder)
//   - power_on    : true to power the VM on after clone
//
// Guest customization (sysprep / cloud-init) is intentionally NOT
// surfaced here — the v1 iteration is clone + rename + power-on only
// (issue #77 §Iteration 2). A future commit extends the request with
// a customization spec + adds the corresponding vCenter flag.
// ============================================================================

using System.Text.Json.Serialization;

namespace Plexor.Providers.VSphere.Provisioning;

/// <summary>
///     Body for <c>POST /api/vcenter/vm?action=clone</c>. Property
///     names match vCenter's wire format exactly — the Refit
///     serializer uses the property name (PascalCase → camelCase
///     default; vCenter accepts camelCase keys).
/// </summary>
public sealed record VSphereCloneRequest
{
    /// <summary>Source template mo-ref (e.g. <c>"vm-42"</c>).</summary>
    [JsonPropertyName("source")]
    public required string SourceTemplateMoref { get; init; }

    /// <summary>Display name for the new VM.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Target VM-folder mo-ref. Null means "default
    /// folder" (the vCenter root VM folder for the chosen
    /// cluster).</summary>
    [JsonPropertyName("folder")]
    public string? TargetFolderMoref { get; init; }

    /// <summary>Power the VM on once the clone completes. The v1
    /// provisioning flow always sets this true — a future
    /// commit can flip it per-request.</summary>
    [JsonPropertyName("power_on")]
    public bool PowerOn { get; init; } = true;
}
