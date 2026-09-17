import { APP_NAME } from './app-name';

/**
 * TanStack Router head() helper. Spreads into route config so each route's
 * `<title>` reads `<page> · ${APP_NAME}` (all lowercase) without per-route
 * boilerplate.
 *
 * TanStack Router's `head()` returns `{ links?, scripts?, meta? }` —
 * `meta` is a `MetaDescriptor[]` and `{ title: string }` is a valid
 * descriptor. Returning `{ title }` at the top level is not assignable,
 * so we wrap the title in `meta: [{ title }]`.
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
      meta: [{ title }],
    }),
  };
}