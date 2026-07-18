import { APP_NAME } from './app-name';

/**
 * TanStack Router head() helper. Spreads into route config so each route's
 * `<title>` reads `Page · ${APP_NAME}` without per-route boilerplate.
 *
 * Usage:
 *   export const Route = createFileRoute('/vms/')({
 *     component: VmsPage,
 *     ...routeHead('VMs'),
 *   });
 */
export function routeHead(page: string | null) {
  return {
    head: () => ({
      title: page ? `${page} · ${APP_NAME}` : APP_NAME,
    }),
  };
}