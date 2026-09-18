// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// InvalidWorkloadLifecycleTransitionException — raised by Workload.Mark*
// methods when an attempted transition isn't in the
// WorkloadLifecycleTransitions table. The handler catches this and
// surfaces it as a 409 Conflict ProblemDetails (the operator asked
// for something that doesn't fit the workload's current state — e.g.
// "Start a Deleted workload").
//
// Lives outside the ClustersException hierarchy because ClustersException
// is sealed — this exception is its own sealed type with a stable Code
// the handler dispatches on. The handler maps the (Code, status) pair
// to ProblemDetails identically to ClustersException.
// ============================================================================

using Plexor.Modules.Clusters.Domain.Entities;

namespace Plexor.Modules.Clusters.Domain.Errors;

/// <summary>
///     Thrown by <c>Workload</c> aggregate methods when the caller
///     attempts an illegal lifecycle transition (e.g. requesting
///     <c>MarkRunning</c> from <c>Pending</c> before <c>MarkProvisioning</c>
///     landed). Mapped to HTTP 409 Conflict by the Clusters
///     exception handler (lives in the Infrastructure layer — see
///     ClustersExceptionHandler for the dispatch + status mapping).
/// </summary>
/// <remarks>
///     The exception carries the attempted <c>(from, to)</c> pair so
///     the ProblemDetails detail string can name both states without
///     the operator having to read the entity separately. The Code is
///     stable (<see cref="ClustersExceptions.InvalidLifecycleTransition" />)
///     so clients branch on Code, not on the detail text.
/// </remarks>
public sealed class InvalidWorkloadLifecycleTransitionException : Exception
{
    /// <summary>
    ///     Stable discriminator code (one of the
    ///     <see cref="ClustersExceptions" /> constants). Identical
    ///     shape to <see cref="ClustersException.Code" /> so the
    ///     exception handler can dispatch on Code uniformly.
    /// </summary>
    public string Code { get; }

    /// <summary>State the workload was in when the rejected transition was attempted.</summary>
    public WorkloadLifecycleState FromState { get; }

    /// <summary>State the caller tried to transition into.</summary>
    public WorkloadLifecycleState ToState { get; }

    /// <summary>
    ///     Constructs an invalid-transition exception. The message is
    ///     safe to surface in ProblemDetails <c>detail</c> (no PII,
    ///     no stack trace).
    /// </summary>
    /// <param name="fromState">Pre-transition state.</param>
    /// <param name="toState">Requested (rejected) post-transition state.</param>
    public InvalidWorkloadLifecycleTransitionException(
        WorkloadLifecycleState fromState,
        WorkloadLifecycleState toState)
        : base($"Illegal workload lifecycle transition: {fromState} → {toState}.")
    {
        Code = ClustersExceptions.InvalidLifecycleTransition;
        FromState = fromState;
        ToState = toState;
    }
}
