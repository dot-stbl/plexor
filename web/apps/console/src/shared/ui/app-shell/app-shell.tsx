import type { ReactNode } from 'react';
import type { LauncherSummaryCard } from '@/mocks/launcher-summary';
import { SidebarProvider } from '@/shared/ui/primitives/sidebar';
import { AppSidebar } from './app-sidebar';
import { AppHeader } from './app-header';

/**
 * App chrome: contextual sidebar (expanded) + slim top bar (scope + breadcrumbs)
 * + scrollable content slot. Account, theme and notifications no longer live in
 * a navbar — account is in the sidebar footer, theme is in the Settings modal.
 *
 * `launcherSummary` is a pass-through prop for `AppSidebar`'s `AppLauncher` —
 * this component never fetches it itself (shared/ never depends on a
 * domain or a mock module; the composition root does, see
 * routes/__root.tsx). Optional so a caller that doesn't care about the
 * launcher's SUMMARY row (e.g. a test rendering just the shell) can omit
 * it — the launcher then simply shows no SUMMARY cards.
 */
export function AppShell({
  children,
  launcherSummary = [],
}: {
  children: ReactNode;
  launcherSummary?: readonly LauncherSummaryCard[];
}) {
  return (
    <SidebarProvider>
      <AppSidebar launcherSummary={launcherSummary} />
      <div className="relative flex min-w-0 flex-1 flex-col bg-background">
        <AppHeader />
        <div className="flex-1 overflow-auto" data-od-id="app-content">
          {children}
        </div>
      </div>
    </SidebarProvider>
  );
}
