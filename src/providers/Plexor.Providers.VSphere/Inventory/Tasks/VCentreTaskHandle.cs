// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VCentreTaskHandle — vCenter long-running task handle wire DTO.
// Init-property record per anti-patterns.md §2 (no positional records
// on wire shapes). Sealed per naming-and-types.md §2.
// ============================================================================

using System.Text.Json.Serialization;

namespace Plexor.Providers.VSphere.Inventory.Tasks;

/// <summary>
///     Raw JSON object — vCenter returns some payloads as opaque
///     objects (e.g. the task handle for a long-running clone).
///     JsonExtensionData catches every field; the inventory mapper
///     reads only what it needs.
/// </summary>
public sealed record VCentreTaskHandle
{
    /// <summary>vCenter task mo-ref (e.g. <c>"task-9876"</c>). Used
    /// by follow-up <c>GET /api/vcenter/tasks/{task}</c> polls.</summary>
    public required string TaskMoref { get; init; }

    /// <summary>Returned VM mo-ref once the clone completes. Only
    /// populated on the post-clone polling result; absent on the
    /// initial POST response.</summary>
    public string? ResultVmMoref { get; init; }

    /// <summary>Task status string — <c>"QUEUED"</c>, <c>"RUNNING"</c>,
    /// <c>"SUCCESS"</c>, <c>"ERROR"</c>.</summary>
    public string? Status { get; init; }

    /// <summary>Extra fields from the upstream task JSON, preserved
    /// for diagnostic logs.</summary>
    [JsonExtensionData]
    public IDictionary<string, object?>? ExtraFields { get; init; }
}
