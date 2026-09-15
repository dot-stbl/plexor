/**
 * Audit types — wire shape for the audit admin endpoints. Mirror of
 * the backend's Plexor.Modules.Audit.Api.Models.AuditQueryResponse.
 * Hand-rolled because the kubb codegen pipeline generates the
 * VM/cluster surfaces only; the audit read surface is a small
 * addition that doesn't justify a kubb regen round-trip.
 *
 * Future migration: replace these hand-rolled types with the
 * kubb-generated Audit types once the FE codegen step is run against
 * the latest OpenAPI spec.
 */

/**
 * One row in the response of GET /api/v1/audit.
 * The shape mirrors `atlas.audit_entries` minus the raw `payload_json`
 * string — the endpoint deserializes the payload into a generic JSON
 * dictionary so the consumer doesn't have to parse string-to-string
 * on every render.
 */
export interface AuditEntry {
  /** AuditEntry row id (UUID v7). */
  id: string;
  /** Stable dot.case wire name of the event (e.g. `quotas.assignment.changed`). */
  action: string;
  /** Tenant scope — every row is denormalized to a single org. */
  orgId: string;
  /** Id of the user (or API-key owner) that triggered the event. */
  actorUserId: string | null;
  /** Short discriminator of what the event refers to (e.g. `quota_assignment`). */
  targetKind: string;
  /** Id of the affected entity. Null when the event doesn't reference a single row. */
  targetId: string | null;
  /**
   * Action-specific context keys parsed from the row's `payload_json`
   * column. A malformed payload falls back to a single `_raw` key
   * with the original string — the endpoint never fails the whole
   * response because one row had a bad payload.
   */
  payload: Record<string, unknown>;
  /** UTC timestamp the event fired. */
  occurredAt: string;
}

/**
 * Query parameters for GET /api/v1/audit. `null` / undefined fields
 * are omitted from the URL — the backend treats them as "no filter".
 *
 * Pagination is offset-based (`before` cursor) for v1 — cursor
 * pagination lands when the volume justifies it. `limit` is clamped
 * to [1, 500] by the backend (default 100).
 */
export interface AuditQueryParams {
  /** Filter to a single action wire name. */
  action?: string | null;
  /** Filter to a single actor (user id). */
  actorUserId?: string | null;
  /** ISO timestamp — only events fired at-or-after this boundary. */
  since?: string | null;
  /** ISO timestamp — only events fired strictly before this boundary. */
  before?: string | null;
  /** Max rows to return (clamped to [1, 500] by the backend). */
  limit?: number;
}
