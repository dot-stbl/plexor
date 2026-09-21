import { createFileRoute, Navigate } from '@tanstack/react-router';

/**
 /docs route — `/(docs)` pathless group's only direct index. Redirects
 to `/docs/getting-started` so anyone hitting the docs section
 (typing `/docs` directly, or following a stale link) lands on the
 first chapter instead of a 404 or a blank page.
 */
export const Route = createFileRoute('/(docs)/docs/')({
  component: () => <Navigate to="/docs/getting-started" />,
});