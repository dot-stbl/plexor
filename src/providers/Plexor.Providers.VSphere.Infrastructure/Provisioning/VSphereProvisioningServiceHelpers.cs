// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereProvisioningServiceHelpers — file-scoped helpers for the
// provisioning service. The handler stays pure orchestration
// (class-layout-and-tooling.md §1a — no private methods); the
// audit-trail writer + the length-capped truncation move out here
// so each piece is testable in isolation.
// ============================================================================

using Plexor.Providers.VSphere.Infrastructure.Persistence;

namespace Plexor.Providers.VSphere.Infrastructure.Provisioning.VSphereProvisioningServiceHelpers;

/// <summary>
///     Audit-trail writer for failed provisioning attempts. Called
///     from the provisioning service's catch blocks (ApiException,
///     HttpRequestException, TaskCanceledException-on-timeout) to
///     persist the FAILED / TIMEOUT row so the operator can
///     reconstruct "who tried to clone what, when".
/// </summary>
internal static class ProvisioningAuditWriter
{
    /// <summary>Length cap on the persisted error message —
    /// matches the column max length in the entity configuration.</summary>
    private const int MaxErrorMessageLength = 2048;

    /// <summary>
    ///     Persist the failure row so the audit trail captures every
    ///     attempted clone — successes and failures alike. The row's
    ///     <c>finished_at</c> is sourced from the injected
    ///     <see cref="TimeProvider" /> so the stamp matches the
    ///     service's <c>started_at</c> + clock skew.
    /// </summary>
    /// <param name="db">The provisioning DbContext. The caller
    /// owns the scope; the helper just calls AddAsync +
    /// SaveChangesAsync.</param>
    /// <param name="clock">TimeProvider for <c>finished_at</c>.</param>
    /// <param name="runId">Audit-trail row id (UUID v7).</param>
    /// <param name="sourceTemplateMoref">Source template mo-ref.</param>
    /// <param name="name">Display name requested for the new VM.</param>
    /// <param name="targetFolderMoref">Target VM-folder mo-ref. Null
    /// when the caller did not specify one.</param>
    /// <param name="startedAt">Wall-clock the clone was issued.</param>
    /// <param name="status">Status string — <c>"FAILED"</c> or
    /// <c>"TIMEOUT"</c>.</param>
    /// <param name="errorMessage">Free-form error message from the
    /// upstream call. Truncated to the persisted column length.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task RecordFailureAsync(
        VSphereDbContext db,
        TimeProvider clock,
        Guid runId,
        string sourceTemplateMoref,
        string name,
        string? targetFolderMoref,
        DateTimeOffset startedAt,
        string status,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var finishedAt = clock.GetUtcNow();

        await db.ProvisioningRuns.AddAsync(new VSphereProvisioningRun
        {
            Id = runId,
            SourceTemplateMoref = sourceTemplateMoref,
            RequestedName = name,
            TargetFolderMoref = targetFolderMoref,
            ResultVmMoref = null,
            Status = status,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            ErrorMessage = Truncate(errorMessage, MaxErrorMessageLength),
            CreatedAt = startedAt,
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Truncate an error message to the persisted column
    /// length so a verbose upstream body doesn't fail the
    /// INSERT.</summary>
    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
