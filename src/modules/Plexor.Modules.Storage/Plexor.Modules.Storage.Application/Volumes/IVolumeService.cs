// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IVolumeService — application-layer facade over the Volume persistence
// boundary. Mirrors IBrandingService (Branding module) — a thin seam
// that the API layer depends on (not on the DbContext directly).
//
// Lifetime: Scoped — shares the per-request DbContext with the controller
// the action handler wraps. Quota enforcement lives in the service
// implementation (4.5.c/d): <c>CreateAsync</c> reserves
// <c>storage.volumes.count</c> + <c>storage.volumes.gb</c> at create
// time; <c>UpdateSizeAsync</c> reserves the growth delta against
// <c>storage.volumes.gb</c> on resize.
// ============================================================================

using Plexor.Modules.Storage.Domain.Entities;

namespace Plexor.Modules.Storage.Application.Volumes;

/// <summary>
///     Application-layer service for the volumes capability. Reads +
///     writes Volume rows at the caller's tenant scope. Tenant
///     scoping is enforced by passing <c>orgId</c> to every query —
///     the service never trusts the caller to filter.
/// </summary>
public interface IVolumeService
{
    /// <summary>
    ///     List every volume row for the given org. Empty when the
    ///     org has no volumes. Ordered by CreatedAt DESC (newest
    ///     first) so the admin table view is chronological.
    /// </summary>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<IReadOnlyList<Volume>> ListAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Fetch the volume row by id. Returns <see langword="null" />
    ///     when no row exists OR the row belongs to a different
    ///     org (cross-tenant lookups surface as null, not as 403 —
    ///     callers can't enumerate org ids).
    /// </summary>
    /// <param name="volumeId">Volume id.</param>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<Volume?> GetAsync(
        Guid volumeId,
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Insert a new volume row. Caller supplies all fields except
    ///     Id (assigned here) + CreatedAt + UpdatedAt (the service's
    ///     injected TimeProvider stamps both).
    /// </summary>
    /// <param name="input">New volume fields (sans id + stamps).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<Volume> CreateAsync(
        NewVolumeInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resize an existing volume. Enforces the
    ///     <c>storage.volumes.gb</c> quota delta against the caller's
    ///     org scope — a grow beyond the effective limit throws
    ///     <see cref="Plexor.Shared.Kernel.Quotas.QuotaExceededException" />
    ///     (the global error handler maps it to 429). Shrinking or
    ///     no-change resizes skip the enforcer entirely (a negative or
    ///     zero delta never fails on a capacity quota).
    /// </summary>
    /// <param name="volumeId">Volume id.</param>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="newSizeGb">New volume size in GiB.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The updated <see cref="Volume" />, or <see langword="null" />
    /// when no row matched (id + org).</returns>
    public Task<Volume?> UpdateSizeAsync(
        Guid volumeId,
        Guid orgId,
        int newSizeGb,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete the volume row. Idempotent — a missing row is a
    ///     no-op (returns <see langword="false" />).
    /// </summary>
    /// <param name="volumeId">Volume id.</param>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns><see langword="true" /> when a row was deleted.</returns>
    public Task<bool> DeleteAsync(
        Guid volumeId,
        Guid orgId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Immutable input for <see cref="IVolumeService.CreateAsync" />.
///     Built from the wire <c>CreateVolumeRequest</c> by the controller's
///     file-static helpers so the service layer stays free of API-shape
///     concerns.
/// </summary>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="ClusterId">Target cluster id.</param>
/// <param name="Name">Volume name (1-128 chars; uniqueness is per cluster).</param>
/// <param name="SizeGb">Volume size in GiB.</param>
public sealed record NewVolumeInput(
    Guid OrgId,
    Guid ClusterId,
    string Name,
    int SizeGb);
