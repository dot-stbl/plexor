// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditQueryResponse — projection of an AuditEntry row returned by
// GET /api/v1/audit. Carries the wire-format columns (id, action,
// org_id, actor_user_id, target_kind, target_id, occurred_at) plus
// a parsed view of payload_json.
//
// The payload column on the wire is a generic
// IReadOnlyDictionary<string, object?> — the action-specific keys
// are documented on each AuditActions constant. A future admin UI
// (5.3) can branch on Action + render a typed view of the payload.
// ============================================================================

namespace Plexor.Modules.Audit.Api.Models;

/// <summary>
///     One row in the response of <c>GET /api/v1/audit</c>. Shape
///     mirrors <c>atlas.audit_entries</c> minus the raw
///     <c>payload_json</c> string — the endpoint deserializes the
///     payload into a generic JSON dictionary so consumers don't
///     have to parse a string on every read.
/// </summary>
/// <remarks>
///     <para><b>Why parse the payload on read.</b> The
///     <c>payload_json</c> column on the wire is the contract the
///     IAuditEmitter writes; storing the raw JSON string keeps the
///     table narrow and the per-action shape free to evolve behind
///     the column. Reading it back as a generic dictionary lets
///     consumers (admin UI 5.3, log aggregators) render the
///     action-specific keys without parsing string-to-string at
///     every page load.</para>
///     <para><b>Defensive parse.</b> A malformed payload (legacy
///     row from before the schema was tightened, or an emitter
///     that wrote a non-JSON object) is surfaced as a single
///     <c>_raw</c> key with the original string — the endpoint
///     never fails the whole response because one row had a bad
///     payload. See
///     <c>AuditQueryEndpoint.ParsePayload</c>.</para>
/// </remarks>
public sealed class AuditQueryResponse
{
    /// <summary>AuditEntry row id (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Stable dot.case wire name of the event
    /// (<c>"quotas.assignment.changed"</c>,
    /// <c>"org.auth_provider.changed"</c>, ...).</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Tenant scope — every row is denormalized to a
    /// single org so admin queries slice without joining.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Id of the user (or API-key owner) that triggered
    /// the event. Null when the event is system-driven.</summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>Short discriminator of what the event refers to
    /// (<c>"quota_assignment"</c>, <c>"quota_usage"</c>,
    /// <c>"org_auth_provider_config"</c>, ...).</summary>
    public string TargetKind { get; init; } = string.Empty;

    /// <summary>Id of the affected entity. Null when the event
    /// doesn't reference a single row.</summary>
    public Guid? TargetId { get; init; }

    /// <summary>Action-specific context keys parsed from the row's
    /// <c>payload_json</c> column. Keys are documented on each
    /// <see cref="Plexor.Shared.Kernel.Audit.AuditActions" />
    /// constant. Falls back to a single <c>_raw</c> entry when
    /// the stored payload is not a JSON object.</summary>
    public IReadOnlyDictionary<string, object?> Payload { get; init; } = new Dictionary<string, object?>();

    /// <summary>UTC timestamp the event fired.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}
