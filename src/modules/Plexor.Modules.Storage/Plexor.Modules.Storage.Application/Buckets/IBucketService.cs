// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IBucketService — application-layer facade over the Bucket persistence
// boundary. Mirrors IVolumeService (sibling in the same module) +
// IBrandingService (sibling in another module). Tenant-scoped via
// orgId parameter; the service never trusts the caller to filter.
// ============================================================================

using Plexor.Modules.Storage.Domain.Entities;

namespace Plexor.Modules.Storage.Application.Buckets;

/// <summary>
///     Application-layer service for the buckets capability. Reads +
///     writes Bucket rows at the caller's tenant scope.
/// </summary>
public interface IBucketService
{
    /// <summary>
    ///     List every bucket row for the given org. Ordered by
    ///     CreatedAt DESC.
    /// </summary>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<IReadOnlyList<Bucket>> ListAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Fetch the bucket row by id. Returns <see langword="null" />
    ///     when no row exists OR the row belongs to a different org.
    /// </summary>
    /// <param name="bucketId">Bucket id.</param>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<Bucket?> GetAsync(
        Guid bucketId,
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Insert a new bucket row. Caller supplies name + region;
    ///     the service stamps CreatedAt + UpdatedAt + assigns the id.
    /// </summary>
    /// <param name="input">New bucket fields.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<Bucket> CreateAsync(
        NewBucketInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete the bucket row. Idempotent — a missing row is a
    ///     no-op.
    /// </summary>
    /// <param name="bucketId">Bucket id.</param>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns><see langword="true" /> when a row was deleted.</returns>
    public Task<bool> DeleteAsync(
        Guid bucketId,
        Guid orgId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Immutable input for <see cref="IBucketService.CreateAsync" />.
///     Built from the wire <c>CreateBucketRequest</c> by the controller's
///     file-static helpers so the service layer stays free of API-shape
///     concerns.
/// </summary>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Name">S3-style bucket name (≤64 chars; uniqueness is per region).</param>
/// <param name="Region">Region label (≤64 chars).</param>
public sealed record NewBucketInput(
    Guid OrgId,
    string Name,
    string Region);
