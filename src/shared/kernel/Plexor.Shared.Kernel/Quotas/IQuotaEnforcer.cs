// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IQuotaEnforcer — atomically reserves capacity for a resource-create
// request. Resource-create handlers across Compute / Storage / Network
// call into this contract; the implementation lives in the Quotas
// Infrastructure layer.
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Atomically reserves capacity for a resource-creation request.
///     The implementation must run inside the same database transaction
///     as the caller's resource INSERT — a failed resource creation
///     must roll back the reservation.
/// </summary>
/// <remarks>
///     <para><b>Why interface in shared kernel.</b> Compute / Storage /
///     Network handlers depend on the contract, not on the EF-backed
///     implementation in <c>Plexor.Modules.Quotas.Infrastructure</c>.
///     The interface in Plexor.Shared.Kernel keeps the dependency
///     direction inward (modules → kernel) and lets unit tests
///     substitute a fake without spinning up Postgres.</para>
///     <para><b>Atomicity contract.</b> The 4.5.b implementation
///     acquires a <c>pg_advisory_xact_lock</c> at scope granularity,
///     upserts the <c>quota_usage</c> row, and conditionally updates
///     <c>current_value</c>. The lock + the UPDATE + the caller's
///     resource INSERT commit (or rollback) together.</para>
/// </remarks>
public interface IQuotaEnforcer
{
    /// <summary>
    ///     Resolves the effective limit for <paramref name="definitionKey" /> at
    ///     <paramref name="scope" />, atomically reserves <paramref name="amount" />,
    ///     and returns <see cref="QuotaCheckResult.Allowed" /> /
    ///     <see cref="QuotaCheckResult.AllowedWithWarning" /> (80% threshold) /
    ///     <see cref="QuotaCheckResult.Denied" />.
    /// </summary>
    /// <param name="scope">Polymorphic scope (org / team / folder) the reservation lands against.</param>
    /// <param name="definitionKey">Stable catalog key (e.g. <c>compute.vms.count</c>).</param>
    /// <param name="amount">How much to add to the current usage (positive decimal).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>
    ///     <see cref="QuotaCheckResult.Allowed" /> when the reservation fits
    ///     inside the effective limit and does not cross the warning threshold;
    ///     <see cref="QuotaCheckResult.AllowedWithWarning" /> when the
    ///     reservation fits but pushes usage past 80% of the limit;
    ///     <see cref="QuotaCheckResult.Denied" /> when the reservation would
    ///     exceed the limit (the caller is responsible for rolling back
    ///     the surrounding transaction).
    /// </returns>
    public Task<QuotaCheckResult> CheckAndReserveAsync(
        QuotaScope scope,
        QuotaDefinitionKey definitionKey,
        decimal amount,
        CancellationToken cancellationToken = default);
}
