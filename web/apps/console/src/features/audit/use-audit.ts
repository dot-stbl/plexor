/**
 * useAudit hooks — TanStack Query surface for the audit
 * /api/v1/audit endpoint. Mirrors the use-branding pattern:
 * `useAudit` returns the rows for the current filter set; the
 * caller wires filters into the queryKey so cache invalidation
 * happens when they change.
 */
import { useQuery } from '@tanstack/react-query';
import { fetchAudit } from './audit-service';
import type { AuditQueryParams } from './audit-types';

/** Query key factory — keeps the cache invalidation paths honest.
 * Filter params are JSON-stringified into the key so a different
 * filter set never reuses a cached page. */
export const auditQueryKeys = {
  list: (params: AuditQueryParams) =>
    ['audit', 'list', JSON.stringify(params)] as const,
};

/**
 * Read the tenant-scoped audit timeline. The query is enabled by
 * default; callers pass null/undefined for "no filter" fields and
 * the hook serialises them out of the queryKey.
 */
export function useAudit(params: AuditQueryParams) {
  return useQuery({
    queryKey: auditQueryKeys.list(params),
    queryFn: () => fetchAudit(params),
    staleTime: 30_000,
  });
}
