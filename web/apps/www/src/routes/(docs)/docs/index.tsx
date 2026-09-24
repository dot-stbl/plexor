import { createFileRoute, redirect } from '@tanstack/react-router';

/**
 /docs route — `/(docs)` pathless group's only direct index. Redirects
 to `/docs/getting-started` so anyone hitting the docs section
 (typing `/docs` directly, or following a stale link) lands on the
 first chapter instead of a 404 or a blank page.

 `beforeLoad` throws the redirect before the route component mounts,
 so there is no flash of 'DocsNotFound' between the bare URL and the
 redirect target. `<Navigate>` would fire in `useEffect` after paint.
 */
export const Route = createFileRoute('/(docs)/docs/')({
  beforeLoad: ({ location }) => {
    if (location.pathname === '/docs' || location.pathname === '/docs/') {
      throw redirect({ to: '/docs/getting-started' });
    }
  },
});