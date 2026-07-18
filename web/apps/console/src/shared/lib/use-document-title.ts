import { useEffect } from 'react';
import { APP_NAME } from './app-name';

/**
 * Set `document.title` to `${page} · ${APP_NAME}` for the lifetime of the
 * component. Restores the previous title on unmount.
 *
 * Use for routes whose page-specific part of the title comes from loaded
 * data (e.g. cluster name from `useGetCluster(id)`). For routes whose
 * title is fully known at definition time, prefer `routeHead()` in the
 * route config — it's static and runs before any data loads.
 *
 * Usage:
 *   const { data: cluster } = useGetCluster(id);
 *   useDocumentTitle(cluster?.name ?? null);
 */
export function useDocumentTitle(page: string | null | undefined): void {
  useEffect(() => {
    const previous = document.title;
    document.title = page ? `${page} · ${APP_NAME}` : APP_NAME;
    return () => {
      document.title = previous;
    };
  }, [page]);
}