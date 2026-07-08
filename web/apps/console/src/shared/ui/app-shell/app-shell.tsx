import type { ReactNode } from 'react';
import { SidebarProvider } from '@/shared/ui/primitives/sidebar';
import { AppSidebar } from './app-sidebar';
import { AppHeader } from './app-header';

/**
 * App chrome: contextual sidebar (expanded) + slim top bar (scope + breadcrumbs)
 * + scrollable content slot. Account, theme and notifications no longer live in
 * a navbar — account is in the sidebar footer, theme is in the Settings modal.
 */
export function AppShell({ children }: { children: ReactNode }) {
  return (
    <SidebarProvider>
      <AppSidebar />
      <div className="relative flex min-w-0 flex-1 flex-col bg-background">
        <AppHeader />
        <div className="flex-1 overflow-auto" data-od-id="app-content">
          {children}
        </div>
      </div>
    </SidebarProvider>
  );
}
