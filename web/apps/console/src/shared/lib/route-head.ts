import { APP_NAME } from './app-name';

/**
 * TanStack Router head() helper. Spreads into route config so each route's
 * `<title>` reads `<page> · ${APP_NAME}` (all lowercase) without per-route
 * boilerplate.
 *
 * Usage:
 *   export const Route = createFileRoute('/vms/')({
 *     component: VmsPage,
 *     ...routeHead('VMs'),
 *   });
 */
export function routeHead(page: string | null) {
  const title = page ? `${page.toLowerCase()} · ${APP_NAME}` : APP_NAME;
  return {
    head: () => ({
      title,
    }),
  };
}