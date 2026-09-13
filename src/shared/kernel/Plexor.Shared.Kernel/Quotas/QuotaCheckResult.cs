// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// QuotaCheckResult — discriminated union returned by IQuotaEnforcer.
// Lives in Plexor.Shared.Kernel because the enforcer contract lives
// there; resource-create handlers across modules pattern-match on
// the concrete variants.
// ============================================================================

namespace Plexor.Shared.Kernel.Quotas;

/// <summary>
///     Outcome of an <c>IQuotaEnforcer.CheckAndReserveAsync</c> call.
///     Discriminated union with three variants — handlers pattern-match
///     on the concrete type to decide the response shape.
/// </summary>
/// <remarks>
///     <para><b>Tiered enforcement.</b> The enforcer returns
///     <see cref="AllowedWithWarning" /> at the 80% threshold (the
///     request succeeds but the caller should be alerted), and
///     <see cref="Denied" /> when the call would exceed the
///     effective limit (the handler throws
///     <c>Plexor.Modules.Quotas.Domain.Errors.QuotaExceededException</c>
///     and the API responds with 429 ProblemDetails).</para>
/// </remarks>
public abstract record QuotaCheckResult
{
    private QuotaCheckResult() { }

    /// <summary>
    ///     The request fits inside the effective limit and does not cross
    ///     the warning threshold. No additional signal required.
    /// </summary>
    public sealed record Allowed : QuotaCheckResult;

    /// <summary>
    ///     The request fits inside the effective limit but pushes usage
    ///     past the warning threshold. The HTTP response carries
    ///     <c>X-Quota-Warning: true</c>; the UI shows a warning badge.
    /// </summary>
    /// <param name="ThresholdPct">The percentage threshold that fired (80, 100, ...).</param>
    /// <param name="Used">Current usage after the reservation.</param>
    /// <param name="Limit">Effective limit value.</param>
    public sealed record AllowedWithWarning(decimal ThresholdPct, decimal Used, decimal Limit)
        : QuotaCheckResult;

    /// <summary>
    ///     The request would exceed the effective limit. The handler
    ///     throws <c>QuotaExceededException</c>; the API responds with
    ///     429 ProblemDetails (<c>code = "quotas.exceeded"</c>) and
    ///     extension fields for <c>limit</c>, <c>used</c>,
    ///     <c>requested</c>.
    /// </summary>
    /// <param name="Limit">Effective limit value.</param>
    /// <param name="Requested">Amount the caller tried to reserve.</param>
    /// <param name="Reason">Short human-readable explanation (returned in the ProblemDetails <c>detail</c>).</param>
    public sealed record Denied(decimal Limit, decimal Requested, string Reason) : QuotaCheckResult;
}
