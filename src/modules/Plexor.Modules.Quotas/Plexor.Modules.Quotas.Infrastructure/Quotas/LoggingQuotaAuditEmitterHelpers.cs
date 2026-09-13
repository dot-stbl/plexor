// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LoggingQuotaAuditEmitterHelpers — file-static helpers pulled out of
// LoggingQuotaAuditEmitter.cs to satisfy the no-private-methods
// convention (class-layout-and-tooling.md §1a). Two helpers:
//   * BeginEventScope — pushes every context field + the wire name into
//     a logger scope so the console formatter / OTLP exporter sees them
//     as structured properties on every line.
//   * LogEvent — emits the message body at the right LogLevel based on
//     the event severity (Warning for UsageExceeded + LimitApproaching,
//     Information for the assignment CRUD events).
// ============================================================================

using Microsoft.Extensions.Logging;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     Helpers for <see cref="LoggingQuotaAuditEmitter" />. Pulled out
///     so the emitter stays free of private methods
///     (class-layout-and-tooling.md §1a).
/// </summary>
internal static class LoggingQuotaAuditEmitterHelpers
{
    /// <summary>
    ///     Push the wire name + every <see cref="QuotaAuditContext" />
    ///     field into a logger scope. Every scope key is dot.case +
    ///     snake_case so log aggregators can filter on them without
    ///     caring about the C# property name. The wire name is keyed
    ///     <c>audit_event</c> — the same field the future
    ///     <c>atlas.audit_entries.action</c> column will carry.
    /// </summary>
    /// <param name="logger">Logger that owns the scope.</param>
    /// <param name="auditEvent">Which quota event fired.</param>
    /// <param name="context">Caller-supplied context payload.</param>
    /// <returns>Disposable scope handle; dispose in the emitter's
    /// using-block.</returns>
    public static IDisposable BeginEventScope(
        ILogger logger,
        QuotaAuditEvent auditEvent,
        QuotaAuditContext context)
    {
        var wireName = auditEvent.WireName();

        return logger.BeginScope(new Dictionary<string, object?>
        {
            ["audit_event"] = wireName,
            ["org_id"] = context.OrgId,
            ["actor_user_id"] = context.ActorUserId,
            ["definition_key"] = context.DefinitionKey,
            ["scope_kind"] = context.ScopeKind,
            ["scope_id"] = context.ScopeId,
            ["used"] = context.Used,
            ["limit"] = context.Limit,
            ["requested"] = context.Requested,
            ["threshold_pct"] = context.ThresholdPct,
            ["assignment_id"] = context.AssignmentId,
        })!;
    }

    /// <summary>
    ///     Emit the audit-event log line at the right severity. The
    ///     message body carries the minimum context to make a line
    ///     scannable (<c>wire_name</c> + <c>definition_key</c> +
    ///     <c>scope_kind/scope_id</c>); structured consumers key off
    ///     the scope properties.
    /// </summary>
    /// <param name="logger">Logger that writes the line.</param>
    /// <param name="auditEvent">Which quota event fired — drives the
    /// LogLevel.</param>
    /// <param name="context">Caller-supplied context payload.</param>
    /// <param name="wireName">Pre-rendered wire name (avoid
    /// recomputing the switch inside the call site).</param>
    public static void LogEvent(
        ILogger logger,
        QuotaAuditEvent auditEvent,
        QuotaAuditContext context,
        string wireName)
    {
        switch (auditEvent)
        {
            case QuotaAuditEvent.UsageExceeded:
            case QuotaAuditEvent.LimitApproaching:
                logger.LogWarning(
                    "Quota audit: {WireName} for {DefinitionKey} on {ScopeKind}/{ScopeId}",
                    wireName,
                    context.DefinitionKey,
                    context.ScopeKind,
                    context.ScopeId);
                break;
            default:
                logger.LogInformation(
                    "Quota audit: {WireName} for {DefinitionKey} on {ScopeKind}/{ScopeId}",
                    wireName,
                    context.DefinitionKey,
                    context.ScopeKind,
                    context.ScopeId);
                break;
        }
    }
}
