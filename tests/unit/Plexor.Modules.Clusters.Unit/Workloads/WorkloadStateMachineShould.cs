// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Workload state-machine tests — exercise Workload.Mark* on a freshly
// constructed aggregate (no DbContext needed; the aggregate is
// pure-domain). Cover the happy path (Pending → Provisioning →
// Stopped → Running → Stopped → Deleting → Deleted), the rejection
// paths (illegal transitions throw
// InvalidWorkloadLifecycleTransitionException), and the audit-trail
// invariant (every successful transition appends a
// WorkloadLifecycleEvent row with the correct (from, to) pair).
// ============================================================================

using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Workloads;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Clusters.Unit.Workloads;

public sealed class WorkloadStateMachineShould
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UtcNow;

    [Fact(DisplayName = "Given Pending workload, when MarkProvisioning, then state advances + provider handle stored + event appended")]
    public void PendingTransitionsToProvisioning()
    {
        var workload = NewWorkload();

        workload.MarkProvisioning("noop-handle-1", T0);

        workload.LifecycleState.ShouldBe(WorkloadLifecycleState.Provisioning);
        workload.ProviderVmId.ShouldBe("noop-handle-1");
        workload.Events.Count.ShouldBe(1);
        workload.Events[0].FromState.ShouldBe(WorkloadLifecycleState.Pending);
        workload.Events[0].ToState.ShouldBe(WorkloadLifecycleState.Provisioning);
        workload.Events[0].ProviderVmId.ShouldBe("noop-handle-1");
        workload.Events[0].OccurredAt.ShouldBe(T0);
    }

    [Fact(DisplayName = "Given Pending workload, when MarkRunning directly, then throws InvalidWorkloadLifecycleTransition")]
    public void PendingCannotJumpToRunning()
    {
        var workload = NewWorkload();

        Should.Throw<InvalidWorkloadLifecycleTransitionException>(
            () => workload.MarkRunning(T0));
    }

    [Fact(DisplayName = "Given Pending → Provisioning → Stopped → Running → Stopped → Deleting → Deleted, every transition succeeds and appends one event")]
    public void HappyPathAdvancesThroughEveryState()
    {
        var workload = NewWorkload();

        workload.MarkProvisioning("noop-handle-1", T0);
        workload.MarkStopped(T0.AddSeconds(1));
        workload.MarkRunning(T0.AddSeconds(2));
        workload.MarkStopped(T0.AddSeconds(3));
        workload.MarkDeleting(T0.AddSeconds(4));
        workload.MarkDeleted(T0.AddSeconds(5));

        workload.LifecycleState.ShouldBe(WorkloadLifecycleState.Deleted);
        workload.Events.Count.ShouldBe(6);

        // Spot-check the (from, to) chain — the full sequence is
        // asserted by the count + the final state.
        var sequence = workload.Events
            .Select(static evt => (evt.FromState, evt.ToState))
            .ToList();
        sequence.ShouldBe(
        [
            (WorkloadLifecycleState.Pending, WorkloadLifecycleState.Provisioning),
            (WorkloadLifecycleState.Provisioning, WorkloadLifecycleState.Stopped),
            (WorkloadLifecycleState.Stopped, WorkloadLifecycleState.Running),
            (WorkloadLifecycleState.Running, WorkloadLifecycleState.Stopped),
            (WorkloadLifecycleState.Stopped, WorkloadLifecycleState.Deleting),
            (WorkloadLifecycleState.Deleting, WorkloadLifecycleState.Deleted)
        ]);
    }

    [Fact(DisplayName = "Given Deleted workload, when MarkProvisioning, then throws (terminal state)")]
    public void DeletedIsTerminal()
    {
        var workload = NewWorkload();
        workload.MarkProvisioning("noop-handle-1", T0);
        workload.MarkStopped(T0.AddSeconds(1));
        workload.MarkDeleting(T0.AddSeconds(2));
        workload.MarkDeleted(T0.AddSeconds(3));

        Should.Throw<InvalidWorkloadLifecycleTransitionException>(
            () => workload.MarkProvisioning("noop-handle-2", T0.AddSeconds(4)));
    }

    [Fact(DisplayName = "Given Failed workload, when MarkDeleting, then advances to Deleting + event carries the failure reason")]
    public void FailedCanTransitionToDeleting()
    {
        var workload = NewWorkload();
        workload.MarkProvisioning("noop-handle-1", T0);
        workload.MarkFailed("provider rejected spec", T0.AddSeconds(1));

        // The Failed event row carries the reason; the LastMessage
        // mirror is also populated. Both are asserted BEFORE the
        // next transition (MarkDeleting clears LastMessage on
        // successful transitions — the reason lives on in the
        // audit-trail event row, not on the aggregate's
        // LastMessage).
        var failingEvent = workload.Events.Single(static evt => evt.ToState == WorkloadLifecycleState.Failed);
        failingEvent.Reason.ShouldBe("provider rejected spec");
        workload.LastMessage.ShouldBe("provider rejected spec");

        workload.MarkDeleting(T0.AddSeconds(2));

        workload.LifecycleState.ShouldBe(WorkloadLifecycleState.Deleting);
    }

    private static Workload NewWorkload()
    {
        var now = DateTimeOffset.UtcNow;
        return new Workload
        {
            Id = IdGenerator.NewWorkloadId(),
            ClusterId = IdGenerator.NewClusterId(),
            Name = $"wl-{Guid.NewGuid():N}",
            Kind = "vm",
            SpecJson = "{}",
            State = WorkloadState.Provisioning,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
