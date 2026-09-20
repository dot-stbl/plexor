// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereProvisioningService — clones a VM template into a target VM
// folder and powers it on. Issue #77 §Iteration 2 — clone + rename +
// power-on only; no guest customization (sysprep / cloud-init) in
// v1.
//
// One method, CloneTemplateAsync. The audit-trail row is written in
// the same scope as the upstream call so a failure on SaveChanges
// surfaces the same way as a Refit failure. The upstream call is
// synchronous (waits for vCenter to report SUCCESS on the clone
// task) — long clones (multi-TiB disks) block the request. A
// future iteration moves the long-running path to a background
// job (issue §"out of scope — async job tracking for long clones").
// ============================================================================

using System.Net.Http;
using Microsoft.Extensions.Logging;
using Plexor.Providers.VSphere;
using Plexor.Providers.VSphere.Infrastructure.Persistence;
using Plexor.Providers.VSphere.Infrastructure.Provisioning.VSphereProvisioningServiceHelpers;
using Plexor.Providers.VSphere.Provisioning;
using Refit;

namespace Plexor.Providers.VSphere.Infrastructure.Provisioning;

/// <summary>
///     Outcome of a single clone attempt. Returned to the API layer
///     so the endpoint handler can shape the success response
///     (200 + the new VM mo-ref) without re-reading the audit
///     row.
/// </summary>
/// <param name="RunId">Audit-trail row id (UUID v7).</param>
/// <param name="VmMoref">vCenter mo-ref for the new VM. Null when
/// the clone failed.</param>
/// <param name="Status"><c>"SUCCESS"</c>, <c>"FAILED"</c>,
/// <c>"TIMEOUT"</c>.</param>
public sealed record VSphereCloneResult(
    Guid RunId,
    string? VmMoref,
    string Status);

/// <summary>
///     Issues a vCenter clone + records the audit trail. The
///     audit row is written even on failure so operators can
///     reconstruct "who tried to clone what" without scraping the
///     vCenter task log.
/// </summary>
public sealed class VSphereProvisioningService(
    IVSphereClient client,
    VSphereDbContext db,
    TimeProvider clock,
    ILogger<VSphereProvisioningService> logger)
{
    /// <summary>
    ///     Issue a clone, persist the audit-trail row, return the
    ///     outcome. Errors from the upstream call surface as
    ///     <see cref="VSphereCloneResult" /> with
    ///     <c>Status = "FAILED"</c> + an error message — the caller
    ///     decides how to map that to an HTTP response.
    /// </summary>
    /// <param name="sourceTemplateMoref">vCenter template mo-ref to
    /// clone from. Resolved by the API handler from the request
    /// (template name → mo-ref).</param>
    /// <param name="name">Display name for the new VM.</param>
    /// <param name="targetFolderMoref">Target VM-folder mo-ref.
    /// Null means "vCenter default".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<VSphereCloneResult> CloneTemplateAsync(
        string sourceTemplateMoref,
        string name,
        string? targetFolderMoref,
        CancellationToken cancellationToken)
    {
        var startedAt = clock.GetUtcNow();
        var runId = Guid.CreateVersion7();

        try
        {
            var handle = await client.CloneTemplateAsync(
                new VSphereCloneRequest
                {
                    SourceTemplateMoref = sourceTemplateMoref,
                    Name = name,
                    TargetFolderMoref = targetFolderMoref,
                    PowerOn = true,
                },
                cancellationToken);

            var finishedAt = clock.GetUtcNow();
            var resultVmMoref = handle.ResultVmMoref;

            await db.ProvisioningRuns.AddAsync(new VSphereProvisioningRun
            {
                Id = runId,
                SourceTemplateMoref = sourceTemplateMoref,
                RequestedName = name,
                TargetFolderMoref = targetFolderMoref,
                ResultVmMoref = resultVmMoref,
                Status = "SUCCESS",
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                CreatedAt = startedAt,
            }, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "vSphere clone succeeded for template {SourceTemplateMoref} → VM {VmMoref}",
                sourceTemplateMoref,
                resultVmMoref ?? "(unknown)");

            return new VSphereCloneResult(runId, resultVmMoref, "SUCCESS");
        }
        catch (ApiException ex)
        {
            await ProvisioningAuditWriter.RecordFailureAsync(
                db,
                clock,
                runId,
                sourceTemplateMoref,
                name,
                targetFolderMoref,
                startedAt,
                "FAILED",
                $"vCenter returned {ex.StatusCode}: {ex.Message}",
                cancellationToken);

            logger.LogWarning(
                ex,
                "vSphere clone failed for template {SourceTemplateMoref} (HTTP {StatusCode})",
                sourceTemplateMoref,
                ex.StatusCode);

            return new VSphereCloneResult(runId, null, "FAILED");
        }
        catch (HttpRequestException ex)
        {
            // Network-layer failure (DNS, TCP reset, TLS handshake,
            // certificate validation error). The clone never
            // reached vCenter — record FAILED with the exception
            // message so the operator can diagnose.
            await ProvisioningAuditWriter.RecordFailureAsync(
                db,
                clock,
                runId,
                sourceTemplateMoref,
                name,
                targetFolderMoref,
                startedAt,
                "FAILED",
                $"network error: {ex.Message}",
                cancellationToken);

            logger.LogWarning(
                ex,
                "vSphere clone failed for template {SourceTemplateMoref} (network error)",
                sourceTemplateMoref);

            return new VSphereCloneResult(runId, null, "FAILED");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            await ProvisioningAuditWriter.RecordFailureAsync(
                db,
                clock,
                runId,
                sourceTemplateMoref,
                name,
                targetFolderMoref,
                startedAt,
                "TIMEOUT",
                "vCenter did not respond within the configured timeout",
                cancellationToken);

            logger.LogWarning(
                ex,
                "vSphere clone timed out for template {SourceTemplateMoref}",
                sourceTemplateMoref);

            return new VSphereCloneResult(runId, null, "TIMEOUT");
        }
    }
}
