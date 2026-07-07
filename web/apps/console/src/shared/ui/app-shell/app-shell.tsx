import { useEffect, useState, type ReactNode } from 'react';
import { SidebarProvider } from '@/shared/ui/primitives/sidebar';
import { AppSidebar } from './app-sidebar';
import { AppNavbar } from './app-navbar';
import { CommandMenu } from './command-menu';

/**
 * App chrome: collapsible sidebar + sticky navbar + scrollable content slot.
 * Routes render their own <main> inside the content slot (SidebarInset is a
 * <main>, so we use a plain inset <div> here to avoid nesting two <main>s).
 */
export function AppShell({ children }: { children: ReactNode }) {
  const [searchOpen, setSearchOpen] = useState(false);

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        setSearchOpen((prev) => !prev);
      }
    }
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, []);

  return (
    <SidebarProvider>
      <AppSidebar />
      <div className="relative flex min-w-0 flex-1 flex-col bg-background">
        <AppNavbar onOpenSearch={() => setSearchOpen(true)} />
        <div className="flex-1 overflow-auto" data-od-id="app-content">
          {children}
        </div>
      </div>
      <CommandMenu open={searchOpen} onOpenChange={setSearchOpen} />
    </SidebarProvider>
  );
}
