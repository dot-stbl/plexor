import { createFileRoute } from '@tanstack/react-router';
import { LoginPage } from '@/features/auth/login-page';
import { routeHead } from '@/shared/lib/route-head';

/**
 * Route wrapper for /login — delegates to the feature component so the
 * route file stays a one-liner that the TanStack file-router codegen
 * can pick up. The component, schema, error mapping, and session
 * storage all live under `@/features/auth` so other entry points (an
 * embedded modal, an iframe'd OIDC callback landing) can reuse them
 * without re-importing the route.
 */
export const Route = createFileRoute('/login')({
  component: LoginPage,
  ...routeHead('Sign in'),
});
