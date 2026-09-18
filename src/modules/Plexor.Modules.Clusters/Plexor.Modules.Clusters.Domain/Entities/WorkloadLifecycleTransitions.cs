// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadLifecycleTransitions — the transition table the Workload
// aggregate consults on every Mark* method. Lives next to Workload
// because (a) no other type needs to know about it, (b) the static
// helper makes the Mark* methods one-liners instead of carrying the
// switch boilerplate inline, (c) it tests in isolation without a
// DbContext (the rules are pure).
//
// Allowed transitions (any other pair raises
// InvalidWorkloadLifecycleTransitionException):
//
//     Pending       ──► Provisioning         (CreateVM confirmed)
//     Provisioning  ──► Stopped              (CreateVM done, not started)
//     Provisioning  ──► Failed               (CreateVM failed)
//     Stopped       ──► Running              (StartVM)
//     Stopped       ──► Failed               (runtime reported failure)
//     Stopped       ──► Deleting             (DeleteVM from Stopped)
//     Running       ──► Stopped              (StopVM)
//     Running       ──► Failed               (runtime reported failure)
//     Running       ──► Deleting             (DeleteVM from Running)
//     Failed        ──► Deleting             (DeleteVM after failure)
//     Deleting      ──► Deleted              (DeleteVM confirmed)
//
// Pending       cannot reach Running / Deleting directly — the row
// must pass through Provisioning first so the provider handle exists.
// Deleted       is terminal — no further transitions accepted.
// ============================================================================

namespace Plexor.Modules.Clusters.Domain.Entities;

/// <summary>
///     Pure transition table for <see cref="WorkloadLifecycleState" />.
///     Lives as a <c>file static class</c> so dev-tooling keeps the
///     rules co-located with the only consumer (Workload.Mark*); no
///     other type should branch on lifecycle validity directly.
/// </summary>
internal static class WorkloadLifecycleTransitions
{
    /// <summary>
    ///     <c>true</c> when <paramref name="from" /> → <paramref name="to" />
    ///     is an allowed transition. The Mark* methods on Workload
    ///     throw <see cref="Errors.InvalidWorkloadLifecycleTransitionException" />
    ///     when this returns <c>false</c>.
    /// </summary>
    public static bool IsAllowed(WorkloadLifecycleState from, WorkloadLifecycleState to)
    {
        return (from, to) switch
        {
            (WorkloadLifecycleState.Pending, WorkloadLifecycleState.Provisioning) => true,
            (WorkloadLifecycleState.Pending, WorkloadLifecycleState.Failed) => true,
            (WorkloadLifecycleState.Provisioning, WorkloadLifecycleState.Stopped) => true,
            (WorkloadLifecycleState.Provisioning, WorkloadLifecycleState.Failed) => true,
            (WorkloadLifecycleState.Stopped, WorkloadLifecycleState.Running) => true,
            (WorkloadLifecycleState.Stopped, WorkloadLifecycleState.Failed) => true,
            (WorkloadLifecycleState.Stopped, WorkloadLifecycleState.Deleting) => true,
            (WorkloadLifecycleState.Running, WorkloadLifecycleState.Stopped) => true,
            (WorkloadLifecycleState.Running, WorkloadLifecycleState.Failed) => true,
            (WorkloadLifecycleState.Running, WorkloadLifecycleState.Deleting) => true,
            (WorkloadLifecycleState.Failed, WorkloadLifecycleState.Deleting) => true,
            (WorkloadLifecycleState.Deleting, WorkloadLifecycleState.Deleted) => true,
            _ => false
        };
    }
}
