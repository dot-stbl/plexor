import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider, createRouter } from '@tanstack/react-router';
import { applyPreset } from '@plexor/ui/themes';
import { DEFAULT_PRESET_ID, presets } from '@plexor/ui/themes';

import { routeTree } from './routeTree.gen';

import './styles.css';

/**
 * Apply the persisted/auto theme preset BEFORE first render so the docs
 * site ships its branded chrome (matched OKLCH values) without a flash.
 * Mirrors console's `applyBootPreset()` (main.tsx). The inline boot
 * script in index.html already stamped `data-theme-mode` and `.dark` for
 * the first paint baseline; here we re-apply the matching preset so
 * any runtime user preference (a value the boot script couldn't see,
 * or a community-installed marketplace theme in a later phase) takes
 * effect before the first frame.
 */
function bootPreset() {
  try {
    const raw = window.localStorage.getItem('plexor-theme');
    const id = raw && presets.some((p) => p.id === raw) ? raw : DEFAULT_PRESET_ID;
    applyPreset(presets.find((p) => p.id === id) ?? presets[0]!);
  } catch {
    // localStorage unavailable — :root tokens from tokens.css carry
    // the first paint; runtime preset application will retry on the
    // first user interaction with the theme picker.
  }
}

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

bootPreset();

createRoot(rootElement).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
);