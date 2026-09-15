// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditEntry — one row per audited action in `atlas.audit_entries`.
//
// Append-only event log: every EmitAsync call inserts a single row, never
// updates. The wire name (Action column) is the stable discriminator —
// consumers (admin UI 5.3, log aggregators, the future retention sweeper)
// branch on Action, never on the row id. PayloadJson carries the action-
// specific context blob (definition_key, scope, used/limit/requested, etc.)
// so the schema stays narrow while the per-event shape evolves behind
// the JSON column.
//
// The `atlas` schema is the architecture-theme name for the audit module;
// the C# entity is `AuditEntry` (concept name, no schema prefix). See
// AGENTS.md §"Naming: architecture theme vs C# concept" for the mapping.
// ============================================================================

using System.Text.Json;
using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Audit.Domain.Entities;

/// <summary>
///     One row in <c>atlas.audit_entries</c>. Stable event-id (UUID v7),
///     wire-name discriminator, tenant scope, optional actor, optional
///     target pointer, and a JSON-encoded context blob.
/// </summary>
/// <remarks>
///     <para><b>Append-only.</b> The retention sweep (5.3) deletes
///     aged rows; nothing in the application path ever UPDATEs an
///     existing row. The DbContext does not track AuditEntry entities
///     for read-modify-write — every EmitAsync inserts a fresh row.</para>
///     <para><b>Filterable.</b> Properties are exposed through the
///     filter DSL (Phase 5.2 admin endpoint) via
///     <see cref="IFilterableEntity" />. Indexes on
///     <c>(org_id, occurred_at desc)</c> +
///     <c>(action, occurred_at desc)</c> back the standard admin queries.
///</para>
///     <para><b>PayloadJson shape is per-event.</b> The wire-name on
///     <see cref="Action" /> tells consumers which keys to expect.
///     New actions may add new keys; the contract is forward-only —
///     existing keys MUST NOT be removed without an event-version
///     bump in the wire name.</para>
/// </remarks>
public sealed class AuditEntry : IFilterableEntity, ICreatedAt
{
    /// <summary>Unique row identifier (UUID v7, PK).</summary>
    public Guid Id { get; init; }

    /// <summary>
    ///     Stable dot.case wire name of the event
    ///     (<c>"quotas.assignment.changed"</c>,
    ///     <c>"org.auth_provider.changed"</c>, ...). Consumers branch
    ///     on this string; never on the row id.
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    ///     Tenant scope — every audit entry is denormalized to a single
    ///     org so tenant-scoped admin queries slice without joining.
    ///     Always populated (audit emission rejects null org).
    /// </summary>
    public Guid OrgId { get; init; }

    /// <summary>
    ///     Id of the user (or API-key owner) that triggered the event.
    ///     Null when the event is system-driven (background sweep, migrator).
    /// </summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>
    ///     Short discriminator of what the event refers to
    ///     (<c>"quota_assignment"</c>, <c>"quota_usage"</c>,
    ///     <c>"org_auth_provider_config"</c>, ...). Bounded-cardinality
    ///     string suitable for a SQL index.
    /// </summary>
    public string TargetKind { get; init; } = string.Empty;

    /// <summary>
    ///     Id of the affected entity (the QuotaAssignment id, the
    ///     OrgAuthProviderConfig row id, ...). Null when the event
    ///     doesn't reference a single row.
    /// </summary>
    public Guid? TargetId { get; init; }

    /// <summary>
    ///     JSON-encoded event-specific context. Serialized via
    ///     <c>System.Text.Json</c> with <see cref="JsonSerializerOptions.Web" />
    ///     — the action-specific keys are documented on each
    ///     <c>AuditActions</c> constant.
    /// </summary>
    public string PayloadJson { get; init; } = string.Empty;

    /// <summary>
    ///     UTC time the event fired. Sourced from the injected
    ///     <see cref="TimeProvider" /> in <c>DbAuditEmitter</c> so the
    ///     stamp is consistent across the request and unit-testable.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    ///     Row creation time. Equals <see cref="OccurredAt" /> in v1
    ///     (the row is never updated), but the field is kept so future
    ///     schema migrations (e.g. an in-row correction path) can
    ///     diverge it without breaking callers.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
