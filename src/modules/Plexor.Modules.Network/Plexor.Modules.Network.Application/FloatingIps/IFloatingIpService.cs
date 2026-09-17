// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IFloatingIpService — application-layer facade over the FloatingIp
// persistence boundary. Mirrors IVolumeService (Storage) — the API
// layer depends on this interface, not on the DbContext.
// ============================================================================

using Plexor.Modules.Network.Domain.Entities;

namespace Plexor.Modules.Network.Application.FloatingIps;

/// <summary>
///     Application-layer service for the floating-IP capability.
///     Reads + writes FloatingIp rows at the caller's tenant scope.
/// </summary>
public interface IFloatingIpService
{
    /// <summary>
    ///     List every floating IP for the given org. Ordered by
    ///     CreatedAt DESC.
    /// </summary>
    public Task<IReadOnlyList<FloatingIp>> ListAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Fetch the floating IP by id. Returns <see langword="null" />
    ///     when no row exists OR the row belongs to a different org.
    /// </summary>
    public Task<FloatingIp?> GetAsync(
        Guid floatingIpId,
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Insert a new floating IP row. Caller supplies all fields
    ///     except Id (assigned here) + the timestamps (stamped from
    ///     the injected TimeProvider).
    /// </summary>
    public Task<FloatingIp> CreateAsync(
        NewFloatingIpInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete the floating IP row. Idempotent — a missing row is
    ///     a no-op (returns <see langword="false" />).
    /// </summary>
    public Task<bool> DeleteAsync(
        Guid floatingIpId,
        Guid orgId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Immutable input for
///     <see cref="IFloatingIpService.CreateAsync" />. Built from the
///     wire <c>CreateFloatingIpRequest</c> by the endpoint so the
///     service stays free of API-shape concerns.
/// </summary>
public sealed record NewFloatingIpInput(
    Guid OrgId,
    Guid ClusterId,
    string Address);
