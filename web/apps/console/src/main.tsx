import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider, createRouter } from '@tanstack/react-router';
import { TooltipProvider } from '@/shared/ui/primitives/tooltip';
import { Toaster } from '@/shared/ui/primitives/sonner';
import { ThemeProvider } from '@/shared/lib/theme-provider';
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

void enableMocking().then(() => {
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
