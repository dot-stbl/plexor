/**
 * renderWithProviders — the single entry point every component test in
 * web/apps/console imports. Bundles the three providers a real TanStack
 * Router route depends on (Query, Router, i18n) plus PreferencesProvider
 * for theme-side effects the page or its children may trigger.
 *
 * Wraps @testing-library/react's `render` so call-sites keep the standard
 * return shape (`{ getByRole, getByText, ... }`) and can use `rerender`
 * from the same object.
 */
import type { ReactElement } from 'react';
import { render, type RenderOptions, type RenderResult } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nextProvider, initReactI18next } from 'react-i18next';
import { Outlet, RouterProvider, createMemoryHistory, createRootRoute, createRoute, createRouter } from '@tanstack/react-router';
import i18next from 'i18next';
import { PreferencesProvider } from '@/shared/lib/preferences-provider';

export interface RenderWithProvidersOptions extends Omit<RenderOptions, 'wrapper'> {
  /** Pre-populated QueryClient (default: a fresh one with `retry: false` for fast tests). */
  queryClient?: QueryClient;
  /** Initial route URL the in-memory router starts at (default: '/'). */
  initialUrl?: string;
}

/**
 * Render a React element with the Query/Router/i18n/Preferences providers
 * wrapped around it. Returns the standard @testing-library/react RenderResult
 * so call-sites can destructure `getByText`, `getByRole`, `rerender`, etc.
 */
export function renderWithProviders(
  ui: ReactElement,
  options: RenderWithProvidersOptions = {},
): RenderResult {
  const {
    queryClient = createTestQueryClient(),
    initialUrl = '/',
    ...rest
  } = options;

  // Build a minimal routeTree: a root route that renders its `Outlet`,
  // and one catch-all child that renders `ui`. The `useNavigate()` hook
  // inside the page needs the router context — this gives it that without
  // pulling in the whole app routeTree.gen.ts.
  const rootRoute = createRootRoute({
    component: () => <Outlet />,
  });
  const testRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '$',
    component: () => ui,
  });
  const routeTree = rootRoute.addChildren([testRoute]);
  // The router type from `createRouter` uses a generic routeTree; the
  // project's `Register` augmentation pins the production router's
  // routeTree type. The test router doesn't need that — cast to `any`
  // here so `RouterProvider` accepts the in-test router without the
  // file-route codegen running against it.
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [initialUrl] }),
  }) as unknown as Parameters<typeof RouterProvider>[0]['router'];

  return render(
    <I18nextProvider i18n={testI18n}>
      <QueryClientProvider client={queryClient}>
        <PreferencesProvider>
          <RouterProvider router={router} />
        </PreferencesProvider>
      </QueryClientProvider>
    </I18nextProvider>,
    rest,
  );
}

function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
      mutations: { retry: false },
    },
  });
}

/**
 * A fresh i18next instance shared across tests. The production app
 * initializes i18n once at module load (see `@/shared/lib/i18n`), but
 * sharing that module-level instance between tests leaks state and
 * depends on browser-only `localStorage` detection. A dedicated
 * createInstance avoids both.
 *
 * Resources are intentionally empty — tests can supply their own keys
 * via `resources` if they need to assert on a specific translation.
 */
const testI18n = i18next.createInstance();

void testI18n.use(initReactI18next).init({
  lng: 'en',
  fallbackLng: 'en',
  defaultNS: 'translation',
  ns: ['translation'],
  resources: { en: { translation: {} } },
  interpolation: { escapeValue: false },
});