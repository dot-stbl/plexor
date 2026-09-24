/**
 * Domain types for the tenant-facing audit timeline. The wire shape comes
 * from the kubb-generated `AuditQueryResponse`; this module re-exports it
 * with a tighter, page-facing name (`AuditEvent`) and a row-local
 * timestamp formatter.
 *
 * Date formatting is intentionally ISO-ish (UTC) — the audit timeline is
 * a fast scan, the admin/audit page uses the same shape, and we don't
 * want localised formatting drift between the two read surfaces.
 */
import type { AuditQueryResponse } from '@/shared/api';

/** A single audit row as the page sees it. */
export type AuditEvent = AuditQueryResponse;

/**
 * Render an audit row's `occurredAt` for the table. Falls back to the
 * raw string when the timestamp is malformed (MSW/cached rows sometimes
 * hold placeholder ISO during transitions).
 */
export function formatAuditTimestamp(iso: string): string {
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) return iso;
  return parsed.toISOString().replace('T', ' ').replace(/\.\d{3}Z$/, 'Z');
}
