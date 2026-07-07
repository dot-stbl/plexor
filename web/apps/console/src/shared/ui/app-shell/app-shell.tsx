import type { ReactNode } from 'react';
import { SidebarProvider } from '@/shared/ui/primitives/sidebar';
import { AppSidebar } from './app-sidebar';
import { AppNavbar } from './app-navbar';

/**
 * App chrome: a permanently-collapsed icon rail + persistent top bar + a
 * scrollable content slot. The sidebar is locked collapsed via controlled
 * `open={false}` with a no-op setter (no toggle, cmd+B disabled).
 * Routes render their own <main>/PageHeader inside the content slot.
 */
export function AppShell({ children }: { children: ReactNode }) {
  return (
    <SidebarProvider open={false} onOpenChange={() => {}}>
      <AppSidebar />
      <div className="relative flex min-w-0 flex-1 flex-col bg-background">
        <AppNavbar />
        <div className="flex-1 overflow-auto" data-od-id="app-content">
          {children}
        </div>
      </div>
    </SidebarProvider>
  );
}
