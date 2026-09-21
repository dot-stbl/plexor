import { Outlet, createRootRoute } from '@tanstack/react-router';

/**
 * Root layout — owns no chrome. The two surfaces (landing `/` and
 * docs `/docs/*`) each render their own header/footer/sidebar inside
 * their own pathless route group. The root route only forwards to the
 * child outlet; the active theme preset is already applied by
 * `applyBootPreset()` in `main.tsx` before this component mounts.
 */
export const Route = createRootRoute({
  component: () => <Outlet />,
});