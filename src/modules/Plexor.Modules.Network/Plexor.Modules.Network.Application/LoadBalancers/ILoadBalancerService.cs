// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ILoadBalancerService — application-layer facade over the
// LoadBalancer persistence boundary. Mirrors IFloatingIpService
// (sibling in the same module) + IVolumeService (Storage).
// ============================================================================

using Plexor.Modules.Network.Domain.Entities;

namespace Plexor.Modules.Network.Application.LoadBalancers;

/// <summary>
///     Application-layer service for the load-balancer capability.
///     Reads + writes LoadBalancer rows at the caller's tenant scope.
/// </summary>
public interface ILoadBalancerService
{
    /// <summary>
    ///     List every load balancer for the given org. Ordered by
    ///     CreatedAt DESC.
    /// </summary>
    public Task<IReadOnlyList<LoadBalancer>> ListAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Fetch the load balancer by id. Returns <see langword="null" />
    ///     when no row exists OR the row belongs to a different org.
    /// </summary>
    public Task<LoadBalancer?> GetAsync(
        Guid loadBalancerId,
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Insert a new load balancer row. Caller supplies all fields
    ///     except Id (assigned here) + the timestamps (stamped from
    ///     the injected TimeProvider).
    /// </summary>
    public Task<LoadBalancer> CreateAsync(
        NewLoadBalancerInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete the load balancer row. Idempotent — a missing row
    ///     is a no-op.
    /// </summary>
    public Task<bool> DeleteAsync(
        Guid loadBalancerId,
        Guid orgId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Immutable input for
///     <see cref="ILoadBalancerService.CreateAsync" />. Built from
///     the wire <c>CreateLoadBalancerRequest</c> by the endpoint so
///     the service stays free of API-shape concerns.
/// </summary>
public sealed record NewLoadBalancerInput(
    Guid OrgId,
    Guid ClusterId,
    string Name,
    LoadBalancerType Type,
    LoadBalancerAlgorithm Algorithm);
