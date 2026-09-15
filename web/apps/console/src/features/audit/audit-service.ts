/**
 * AuditService — typed wrapper around the backend's GET /api/v1/audit
 * endpoint. Hand-rolled because the kubb codegen pipeline generates the
 * VM/cluster surfaces only; the audit read surface is a small
 * addition that doesn't justify a kubb regen round-trip.
 *
 * Future migration: replace with the kubb-generated client + useQuery
 * once the FE codegen step is run against the latest OpenAPI spec.
 *
 * The runtime base URL is read from `import.meta.env.VITE_API_BASE_URL`;
 * when unset (dev with mocks) the calls fall through to relative
 * paths (Vite proxy in dev, reverse proxy / k8s ingress in prod).
 */
import type { AuditEntry, AuditQueryParams } from './audit-types';

/** Read once at module load. Falls back to relative path (proxy)
 * so the same code works in dev (Vite proxy) + production (reverse
 * proxy / k8s ingress). */
const baseUrl = (() => {
  const explicit = import.meta.env.VITE_API_BASE_URL;
  return (explicit ?? '').replace(/\/$/, '');
})();

/** Build the full URL for the audit endpoint with a query string. */
function buildAuditUrl(params: AuditQueryParams): string {
  const search = new URLSearchParams();
  if (params.action) search.set('action', params.action);
  if (params.actorUserId) search.set('actorUserId', params.actorUserId);
  if (params.since) search.set('since', params.since);
  if (params.before) search.set('before', params.before);
  if (params.limit !== undefined) search.set('limit', String(params.limit));
  const query = search.toString();
  return `${baseUrl}/api/v1/audit${query ? `?${query}` : ''}`;
}

/** Throws on non-2xx so TanStack Query surfaces the error to the UI.
 * The body is best-effort JSON; non-JSON errors fall through with a
 * generic message. */
async function request<T>(url: string): Promise<T> {
  const response = await fetch(url, {
    credentials: 'include',
  });
  if (!response.ok) {
    let detail: string;
    try {
      const problem = (await response.json()) as { detail?: string; title?: string };
      detail = problem.detail ?? problem.title ?? response.statusText;
    } catch {
      detail = response.statusText;
    }
    throw new Error(`Audit fetch failed (${response.status}): ${detail}`);
  }
  return (await response.json()) as T;
}

/**
 * GET /api/v1/audit — tenant-scoped audit timeline. The backend
 * forces `org_id = caller.tenant_id`, so a caller in Org X cannot
 * enumerate Org Y's rows.
 *
 * Returns a list of rows ordered by `occurredAt` DESC. Use the
 * `before` query parameter for offset-style pagination — pass the
 * oldest row's `occurredAt` from the previous page as the next
 * `before` boundary.
 */
export function fetchAudit(params: AuditQueryParams): Promise<AuditEntry[]> {
  return request<AuditEntry[]>(buildAuditUrl(params));
}
