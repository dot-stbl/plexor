// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereProvisioningRun — audit-trail row for a single template
// clone in `outpost.vsphere_provisioning_runs`. Records the
// template mo-ref + target name + folder + the returned VM mo-ref
// + the wall-clock stamp. One row per call to the provisioning
// endpoint — even when the upstream vCenter call fails (Status
// captures the failure shape).
//
// The row lets operators reconstruct "who cloned what, when" without
// scraping the vCenter task log. A future iteration joins the row to
// the tenant Folder id + actor user id (Phase 2 — when the rename
// to Org/Team/Folder completes).
// ============================================================================

using Plexor.Shared.Kernel.Common;

namespace Plexor.Providers.VSphere.Infrastructure.Persistence;

/// <summary>
///     One row per call to the vSphere provisioning endpoint.
///     Append-only — no updates after the upstream clone completes.
/// </summary>
public sealed class VSphereProvisioningRun : ICreatedAt
{
    /// <summary>Run id (UUID v7, PK).</summary>
    public Guid Id { get; init; }

    /// <summary>Source template mo-ref.</summary>
    public string SourceTemplateMoref { get; init; } = string.Empty;

    /// <summary>Display name requested for the new VM.</summary>
    public string RequestedName { get; init; } = string.Empty;

    /// <summary>Target VM-folder mo-ref. Null when the caller
    /// did not specify a folder (vCenter default).</summary>
    public string? TargetFolderMoref { get; init; }

    /// <summary>Returned VM mo-ref on success. Null when the
    /// upstream clone failed.</summary>
    public string? ResultVmMoref { get; init; }

    /// <summary>Status — <c>"SUCCESS"</c>, <c>"FAILED"</c>,
    /// <c>"TIMEOUT"</c>. The handler maps Refit / HTTP failures
    /// onto these three buckets.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>UTC wall-clock the clone was issued.</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>UTC wall-clock the clone finished (or the handler
    /// timed out). Null when the clone is still in flight.</summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>Error message from the upstream call when
    /// <see cref="Status" /> = <c>"FAILED"</c> or
    /// <c>"TIMEOUT"</c>. Bounded length to keep the audit
    /// table small.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>UTC row-creation time. Equals
    /// <see cref="StartedAt" /> in v1 (the row is never updated);
    /// kept for forward compatibility with a future
    /// in-row-correction path.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
