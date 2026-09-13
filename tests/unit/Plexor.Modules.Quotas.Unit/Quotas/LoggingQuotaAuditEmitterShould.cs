// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoggingQuotaAuditEmitterShould — exercise the v1 IQuotaAuditEmitter in
// isolation. No DB needed; the emitter wraps a structured logger. The
// four tests here pin:
//
//   1. AssignmentChanged + AssignmentRemoved → Information level.
//   2. UsageExceeded → Warning level.
//   3. LimitApproaching → Warning level.
//   4. A logging-throwing logger does not propagate — EmitAsync swallows.
//
// All four tests share a tiny in-memory ILogger<T> implementation
// (RecordingLogger) that captures the last log entry's level + message
// + scope dictionary, plus an optional failure mode that throws on the
// next BeginScope to assert the swallow path.
// ============================================================================

using Microsoft.Extensions.Logging;
using Plexor.Modules.Quotas.Infrastructure.Quotas;
using Plexor.Shared.Kernel.Quotas;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Quotas.Unit.Quotas;

/// <summary>
///     Behavioural tests for <see cref="LoggingQuotaAuditEmitter" />.
///     The emitter wraps an <see cref="ILogger{T}" />; the tests assert
///     the level + wire-name mapping, plus the no-throw contract on a
///     logger failure.
/// </summary>
public sealed class LoggingQuotaAuditEmitterShould
{
    /// <summary>
    ///     Given <see cref="QuotaAuditEvent.AssignmentChanged" />, when
    ///     <see cref="LoggingQuotaAuditEmitter.EmitAsync" /> runs, then
    ///     a single log line at <see cref="LogLevel.Information" /> is
    ///     written with the wire name <c>quotas.assignment.changed</c>.
    /// </summary>
    [Fact(DisplayName = "Given AssignmentChanged, when EmitAsync runs, then writes Information with wire name")]
    public async Task EmitAsync_WithAssignmentChanged_WritesInformationLevelAsync()
    {
        var recorder = new RecordingLogger();
        var sut = new LoggingQuotaAuditEmitter(recorder);
        var context = SampleContext();

        await sut.EmitAsync(QuotaAuditEvent.AssignmentChanged, context, CancellationToken.None);

        recorder.LastLevel.ShouldBe(LogLevel.Information);
        recorder.LastFormattedMessage.ShouldContain("quotas.assignment.changed");
        recorder.LastScope.ShouldNotBeNull();
        recorder.LastScope!["audit_event"].ShouldBe("quotas.assignment.changed");
        recorder.LastScope["org_id"].ShouldBe(context.OrgId);
        recorder.LastScope["actor_user_id"].ShouldBe(context.ActorUserId);
        recorder.LastScope["definition_key"].ShouldBe(context.DefinitionKey);
        recorder.LastScope["scope_kind"].ShouldBe(context.ScopeKind);
        recorder.LastScope["scope_id"].ShouldBe(context.ScopeId);
        recorder.LastScope["assignment_id"].ShouldBe(context.AssignmentId);
    }

    /// <summary>
    ///     Given <see cref="QuotaAuditEvent.AssignmentRemoved" />, when
    ///     <see cref="LoggingQuotaAuditEmitter.EmitAsync" /> runs, then
    ///     a single log line at <see cref="LogLevel.Information" /> is
    ///     written with the wire name <c>quotas.assignment.removed</c>.
    ///     Pairs with the AssignmentChanged test — both CRUD events
    ///     share the Information level.
    /// </summary>
    [Fact(DisplayName = "Given AssignmentRemoved, when EmitAsync runs, then writes Information with wire name")]
    public async Task EmitAsync_WithAssignmentRemoved_WritesInformationLevelAsync()
    {
        var recorder = new RecordingLogger();
        var sut = new LoggingQuotaAuditEmitter(recorder);
        var context = SampleContext(assignmentId: Guid.NewGuid());

        await sut.EmitAsync(QuotaAuditEvent.AssignmentRemoved, context, CancellationToken.None);

        recorder.LastLevel.ShouldBe(LogLevel.Information);
        recorder.LastFormattedMessage.ShouldContain("quotas.assignment.removed");
        recorder.LastScope!["audit_event"].ShouldBe("quotas.assignment.removed");
    }

    /// <summary>
    ///     Given <see cref="QuotaAuditEvent.UsageExceeded" />, when
    ///     <see cref="LoggingQuotaAuditEmitter.EmitAsync" /> runs, then
    ///     a single log line at <see cref="LogLevel.Warning" /> is
    ///     written with the wire name <c>quotas.usage.exceeded</c> and
    ///     the scope carries the limit / used / requested numbers.
    /// </summary>
    [Fact(DisplayName = "Given UsageExceeded, when EmitAsync runs, then writes Warning with wire name + numeric scope")]
    public async Task EmitAsync_WithUsageExceeded_WritesWarningLevelAsync()
    {
        var recorder = new RecordingLogger();
        var sut = new LoggingQuotaAuditEmitter(recorder);
        var context = SampleContext(used: 100m, limit: 100m, requested: 1m);

        await sut.EmitAsync(QuotaAuditEvent.UsageExceeded, context, CancellationToken.None);

        recorder.LastLevel.ShouldBe(LogLevel.Warning);
        recorder.LastFormattedMessage.ShouldContain("quotas.usage.exceeded");
        recorder.LastScope!["audit_event"].ShouldBe("quotas.usage.exceeded");
        recorder.LastScope["used"].ShouldBe(100m);
        recorder.LastScope["limit"].ShouldBe(100m);
        recorder.LastScope["requested"].ShouldBe(1m);
    }

    /// <summary>
    ///     Given <see cref="QuotaAuditEvent.LimitApproaching" />, when
    ///     <see cref="LoggingQuotaAuditEmitter.EmitAsync" /> runs, then
    ///     a single log line at <see cref="LogLevel.Warning" /> is
    ///     written with the wire name <c>quotas.limit.approaching</c>
    ///     and the scope carries the threshold percentage.
    /// </summary>
    [Fact(DisplayName = "Given LimitApproaching, when EmitAsync runs, then writes Warning with threshold percentage")]
    public async Task EmitAsync_WithLimitApproaching_WritesWarningLevelAsync()
    {
        var recorder = new RecordingLogger();
        var sut = new LoggingQuotaAuditEmitter(recorder);
        var context = SampleContext(
            used: 85m,
            limit: 100m,
            requested: 5m,
            thresholdPct: 80m);

        await sut.EmitAsync(QuotaAuditEvent.LimitApproaching, context, CancellationToken.None);

        recorder.LastLevel.ShouldBe(LogLevel.Warning);
        recorder.LastFormattedMessage.ShouldContain("quotas.limit.approaching");
        recorder.LastScope!["audit_event"].ShouldBe("quotas.limit.approaching");
        recorder.LastScope["threshold_pct"].ShouldBe(80m);
        recorder.LastScope["used"].ShouldBe(85m);
        recorder.LastScope["limit"].ShouldBe(100m);
    }

    /// <summary>
    ///     Given a logger that throws on <see cref="ILogger.BeginScope" />,
    ///     when <see cref="LoggingQuotaAuditEmitter.EmitAsync" /> runs,
    ///     then the call completes without throwing and the failure is
    ///     recorded at <see cref="LogLevel.Critical" /> (the second log
    ///     line — the swallow). The audit contract is fire-and-forget:
    ///     an emitter failure must never break a user request.
    /// </summary>
    [Fact(DisplayName = "Given a logger that throws on BeginScope, when EmitAsync runs, then does not throw")]
    public async Task EmitAsync_DoesNotThrow_OnLoggingFailureAsync()
    {
        var throwingLogger = new RecordingLogger { ThrowOnBeginScope = true };
        var sut = new LoggingQuotaAuditEmitter(throwingLogger);
        var context = SampleContext();

        // Should not throw — audit emission failure must be swallowed.
        await sut.EmitAsync(QuotaAuditEvent.AssignmentChanged, context, CancellationToken.None);

        // The Critical swallow-log fires; the original emit never wrote
        // a line so LastLevel is the swallow (Critical).
        throwingLogger.LastLevel.ShouldBe(LogLevel.Critical);
        throwingLogger.LastFormattedMessage.ShouldContain("QuotaAuditEmitter");
        throwingLogger.LastFormattedMessage.ShouldContain("quotas.assignment.changed");
    }

    private static QuotaAuditContext SampleContext(
        Guid? assignmentId = null,
        decimal? used = null,
        decimal? limit = null,
        decimal? requested = null,
        decimal? thresholdPct = null)
    {
        return new QuotaAuditContext(
            OrgId: Guid.NewGuid(),
            ActorUserId: Guid.NewGuid(),
            DefinitionKey: "compute.vms.count",
            ScopeKind: "Org",
            ScopeId: Guid.NewGuid(),
            Used: used,
            Limit: limit,
            Requested: requested,
            ThresholdPct: thresholdPct,
            AssignmentId: assignmentId ?? Guid.NewGuid());
    }

    /// <summary>
    ///     Minimal <see cref="ILogger{TCategoryName}" /> that captures
    ///     the last entry's level + formatted message + scope dictionary.
    ///     An optional <see cref="ThrowOnBeginScope" /> switch turns the
    ///     logger into a failure source so the swallow path can be
    ///     exercised. No log provider / no formatter needed — tests
    ///     assert on the captured state directly.
    /// </summary>
    private sealed class RecordingLogger : ILogger<LoggingQuotaAuditEmitter>
    {
        /// <summary>
        ///     When <see langword="true" />, the next
        ///     <see cref="BeginScope{TState}" /> call throws an
        ///     <see cref="InvalidOperationException" /> — used by the
        ///     "must not throw" test to exercise the swallow path.
        /// </summary>
        public bool ThrowOnBeginScope { get; set; }

        public LogLevel LastLevel { get; private set; }

        public string LastFormattedMessage { get; private set; } = string.Empty;

        public Dictionary<string, object?>? LastScope { get; private set; }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (ThrowOnBeginScope)
            {
                throw new InvalidOperationException("synthetic logger scope failure");
            }

            if (state is IReadOnlyCollection<KeyValuePair<string, object?>> entries)
            {
                LastScope = entries.ToDictionary(entry => entry.Key, entry => entry.Value);
            }

            return new NoOpDisposable();
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LastLevel = logLevel;
            LastFormattedMessage = formatter(state, exception);
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
