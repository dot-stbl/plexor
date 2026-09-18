// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaExceededException — thrown by resource-create handlers when the
// quota enforcer returns Denied. Maps to HTTP 429 ProblemDetails
// with code = "quotas.exceeded" and extension fields for limit, used,
// requested.
//
// Lives in Plexor.Shared.Kernel (next to IQuotaEnforcer / QuotaScope /
// QuotaCheckResult) so Compute / Storage / Network handlers can throw
// it without taking a dependency on Plexor.Modules.Quotas.Domain.
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Thrown by handlers when <see cref="QuotaCheckResult.Denied" />
///     is returned by the enforcer. Maps to HTTP 429 ProblemDetails
///     with <c>code = "quotas.exceeded"</c> and extension fields
///     <c>limit</c>, <c>used</c>, <c>requested</c>.
/// </summary>
/// <remarks>
///     <para><b>Carries the enforcer's numbers.</b> The handler fills
///     the ProblemDetails <c>extensions</c> from the three numeric
///     properties so the dashboard can render
///     <c>"100 of 256 vCPU used; requested 16 more"</c> without a
///     second round-trip.</para>
/// </remarks>
/// <remarks>Construct a quota-exceeded error from the enforcer's
/// observed numbers.</remarks>
/// <param name="limit">Effective limit (denormalized into the
/// ProblemDetails <c>extensions.limit</c> field).</param>
/// <param name="used">Observed usage (denormalized into
/// <c>extensions.used</c>).</param>
/// <param name="requested">Amount requested (denormalized into
/// <c>extensions.requested</c>).</param>
public sealed class QuotaExceededException(decimal limit, decimal used, decimal requested) : QuotaException(
        QuotaExceptions.Exceeded,
        $"quota exceeded: used {used} of {limit}, requested {requested}")
{
    /// <summary>Effective limit the call would have exceeded.</summary>
    public decimal Limit { get; } = limit;

    /// <summary>Usage value observed at the time of the call (post-reserve).</summary>
    public decimal Used { get; } = used;

    /// <summary>Amount the caller tried to reserve.</summary>
    public decimal Requested { get; } = requested;
}
