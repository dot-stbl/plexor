import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider, createRouter } from '@tanstack/react-router';

import { routeTree } from './routeTree.gen';

import './styles.css';
import { applyBootPreset } from './lib/apply-theme';

/**
 * Apply the persisted/auto theme preset BEFORE first render so the docs
 * site ships its branded chrome (matched OKLCH values) without a flash.
 * Mirrors the inline boot script in `index.html` — the inline script
 * stamps `.dark` and `data-theme-mode` for the no-FOUC first paint;
 * here we apply the full token set so the full Plexor DS vocabulary
 * (surfaces, ink, borders, status semantics) lands on the root element
 * before any React component reads it. Both paths use the same
 * localStorage key (`plexor-theme`) so they agree.
 */
applyBootPreset();

const router = createRouter({
  routeTree,
  defaultPreload: 'intent',
});

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}

const rootElement = document.getElementById('root');
if (!rootElement) throw new Error('Root element #root not found');

createRoot(rootElement).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
);