import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider, createRouter } from '@tanstack/react-router';
import { TooltipProvider } from '@/shared/ui/primitives/tooltip';
import { Toaster } from '@/shared/ui/primitives/sonner';
import { ThemeProvider } from '@/shared/lib/theme-provider';
import { getBootConfig } from '@/shared/lib/config';
import '@/shared/lib/i18n';
import { routeTree } from './routeTree.gen';

import './index.css';

// Theme bootstrap — apply persisted/auto theme BEFORE first render
// to avoid flash. Reads from the preferences storage key
// ('plexor-preferences') and falls back to the legacy 'plexor-theme'
// key for users who had a value set before the migration.
(function applyThemeEarly() {
  try {
    var raw =
      localStorage.getItem('plexor-preferences') ||
      localStorage.getItem('plexor-theme');
    var theme;
    if (raw) {
      try {
        // New format: JSON object { theme, accent, fontSize }.
        var parsed = JSON.parse(raw);
        theme = parsed && parsed.theme;
      } catch (_) {
        // Legacy format: bare string ('light' | 'dark' | 'system').
        theme = raw;
      }
    }
    var prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    var resolved = theme || (prefersDark ? 'dark' : 'light');
    if (resolved === 'dark') {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }
  } catch (_) {
    // localStorage unavailable — fall back to system preference
    if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
      document.documentElement.classList.add('dark');
    }
  }
})();

// Operator-controlled brand — swap the boot favicon in if the host
// shipped one, and override --accent from `branding.global.customAccent`
// when the operator picked a non-default accent. Both are synchronous
// window-state mutations done before React mounts so the first paint
// already has the operator's icon + accent (no flash from default →
// custom). The accent override is on top of whatever the user later
// picks in PreferencesProvider — operator default, user override wins.
(function applyBootBranding() {
  try {
    var boot = getBootConfig();
    if (boot.brand.faviconUrl) {
      var link = document.getElementById('favicon-link');
      if (link) {
        link.setAttribute('href', boot.brand.faviconUrl);
      }
    }
    var customAccent = boot.branding && boot.branding.global && boot.branding.global.customAccent;
    if (customAccent) {
      document.documentElement.style.setProperty('--accent', customAccent);
    }
  } catch (_) {
    // Boot config unavailable — the default favicon (set in index.html)
    // and the default accent (from index.css / the active preset) stay
    // in place.
  }
})();

// Operator custom CSS escape hatch — probe HEAD /custom.css and enable
// the placeholder link in index.html when the file exists. The
// cache-busting ?v=Date.now() suffix forces a fresh fetch on every
// reload so theme changes in custom.css are picked up at the next
// page load. Async; failure is silent (404 = no custom.css = no link).
(function enableCustomCssAsync() {
  var link = document.getElementById('custom-css-link');
  if (link === null) return;
  fetch('/custom.css', { method: 'HEAD' })
    .then(function (response) {
      if (!response.ok) return;
      if (link === null) return;
      link.setAttribute('href', '/custom.css?v=' + Date.now());
      link.removeAttribute('disabled');
    })
    .catch(function () {
      // Network error — leave the link disabled (no-op).
    });
})();

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
});

const router = createRouter({
  routeTree,
  context: { queryClient },
  defaultPreload: 'intent',
});

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}

const rootElement = document.getElementById('root');
if (!rootElement) throw new Error('Root element #root not found');

// In dev with VITE_USE_MOCKS=true, start MSW (kubb faker-backed handlers) before
// first render. Flip the flag off to hit the real Plexor.Host API — no screen changes.
async function enableMocking() {
  if (import.meta.env.VITE_USE_MOCKS !== 'true') return;
  const { worker } = await import('@/shared/api/mocks/browser');
  await worker.start({ onUnhandledRequest: 'bypass' });
}

// Apply operator-configured theme preset before first render. The default
// preset (`plexor-default-light`) is already in :root from index.css, so
// only non-default operators see this work — and they tolerate a brief
// flash since they opted in. Dynamic imports break what would otherwise
// be a static edge between this file and the theme registry; the
// registry pulls in presets.ts, which only main.tsx ever imports.
async function applyBootPreset() {
  const presetId = getBootConfig().theme.defaultPresetId;
  if (!presetId || presetId === 'plexor-default-light') return;
  try {
    const { getPreset } = await import('@/shared/lib/themes/registry');
    const { applyPreset } = await import('@/shared/lib/themes/apply-tokens');
    const preset = getPreset(presetId);
    if (preset) {
      applyPreset(preset);
    }
  } catch (err) {
    console.warn('boot: failed to apply theme preset', presetId, err);
  }
}

// Community-theme activation — runs as a parallel bootstrap task with
// `applyBootPreset`. Fetches the per-org marketplace installation
// from the kubb-generated `getBrandingTheme` client and applies the
// tokens before first paint so the page renders under the chosen
// palette with no flash. Idempotent: a 404 (no theme activated
// yet) is a no-op.
async function applyBootCommunityTheme() {
  try {
    const { getBrandingTheme } = await import('@/shared/api');
    const { listCommunityThemes } = await import('@/shared/lib/themes/registry');
    const { applyPreset } = await import('@/shared/lib/themes/apply-tokens');
    const installation = await getBrandingTheme();
    const themes = listCommunityThemes();
    const theme = themes.find((entry) => entry.id === installation.themeId);
    if (theme) {
      applyPreset(theme);
    }
  } catch (err) {
    // No marketplace theme installed (404), or the backend is
    // unreachable. Either way: stay on the operator default;
    // don't crash the boot.
    console.warn('boot: failed to apply community theme', err);
  }
}

void Promise.all([enableMocking(), applyBootPreset(), applyBootCommunityTheme()]).then(() => {
  createRoot(rootElement).render(
    <StrictMode>
      <ThemeProvider defaultTheme="system" storageKey="plexor-preferences">
        <QueryClientProvider client={queryClient}>
          <TooltipProvider>
            <RouterProvider router={router} />
            <Toaster />
          </TooltipProvider>
        </QueryClientProvider>
      </ThemeProvider>
    </StrictMode>,
  );
});
