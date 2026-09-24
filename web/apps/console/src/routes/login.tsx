import { createFileRoute } from '@tanstack/react-router';
import { LoginPage } from '@/domains/identity';
import { routeHead } from '@/shared/lib/route-head';

/**
 * Route wrapper for /login — delegates to the domain component so the
 * route file stays a one-liner that the TanStack file-router codegen
 * can pick up. The component, schema, and error mapping live under
 * `@/domains/identity`; the session storage mechanism is shared kernel
 * (`@/shared/lib/session`) — so other entry points (an embedded modal,
 * an iframe'd OIDC callback landing) can reuse them without
 * re-importing the route.
 */
export const Route = createFileRoute('/login')({
  component: LoginPage,
  ...routeHead('Sign in'),
});
