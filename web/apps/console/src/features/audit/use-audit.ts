/**
 * useAudit hook — TanStack Query surface for the audit timeline,
 * backed by the kubb-generated client + hook. Filters are wired into
 * the kubb queryKey via the params arg, so a different filter set
 * never reuses a cached page (kubb composes the key from the URL +
 * the params object).
 */
import { useGetAudit, type GetAuditQueryParams } from '@/shared/api';

/** Query key factory — passes the params straight through to the
 * kubb helper, so a different filter set produces a distinct cache
 * entry. The kubb key shape is `[{ url: '/audit' }, params]`. */
export const auditQueryKeys = {
  list: (params: GetAuditQueryParams) => [{ url: '/audit' }, params] as const,
};

/**
 * Read the tenant-scoped audit timeline. The query is enabled by
 * default; callers pass `null` / `undefined` for "no filter" fields
 * and the kubb serialises them out of the queryKey / query string.
 */
export function useAudit(params: GetAuditQueryParams) {
  return useGetAudit(params, {
    query: {
      staleTime: 30_000,
    },
  });
}
