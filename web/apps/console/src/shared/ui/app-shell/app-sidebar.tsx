import { Fragment, useEffect, useState } from 'react';
import { Link, useRouterState } from '@tanstack/react-router';
import { MagnifyingGlass, SquaresFour } from '@phosphor-icons/react';
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarSeparator,
} from '@/shared/ui/primitives/sidebar';
import { navSections, isActiveRoute } from './nav-config';
import { SearchCommand } from './search-command';
import { AppLauncher } from './app-launcher';
import { cn } from '@/lib/utils';

/** Label pill revealed on rail hover; the hovered item's pill nudges toward its button. */
const railPill =
  'pointer-events-none absolute left-full top-1/2 z-50 ml-3.5 -translate-y-1/2 translate-x-1 whitespace-nowrap rounded-md bg-foreground/70 px-2 py-1 text-xs font-medium text-background opacity-0 shadow-sm backdrop-blur-md transition-all duration-150 ease-out group-hover/rail:translate-x-0 group-hover/rail:opacity-100 group-hover/menu-item:ml-2.5 group-hover/menu-item:bg-foreground/80';

/**
 * Permanently-collapsed icon rail (non-expandable — see AppShell's controlled
 * `open={false}`). Hovering the rail reveals a label pill next to every icon.
 *
 * Structure: a top "main" group (brand launcher + search) is divided from the
 * navigation groups by separators. Items inside a group sit closer together
 * (gap-1) than groups are from each other (separator + margin).
 */
export function AppSidebar() {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const [searchOpen, setSearchOpen] = useState(false);
  const [launcherOpen, setLauncherOpen] = useState(false);

  // ⌘K / Ctrl+K opens the search modal.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'k' && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        setSearchOpen((prev) => !prev);
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, []);

  return (
    <>
      {/* group/rail: hovering anywhere on the rail reveals all label pills. */}
      <Sidebar collapsible="icon" data-od-id="app-sidebar" className="group/rail">
        <SidebarHeader className="gap-1">
          <SidebarMenu className="gap-1">
            {/* Brand launcher — solid token-colored tile (replaces the logo mark), links home. */}
            <SidebarMenuItem className="group/menu-item relative">
              <Link
                to="/"
                aria-label="Plexor — на главную"
                className="flex size-8 items-center justify-center rounded-[calc(var(--radius-sm)+2px)] bg-primary text-sm font-semibold text-primary-foreground outline-none transition-colors hover:bg-primary/90 focus-visible:ring-2 focus-visible:ring-sidebar-ring"
              >
                P
              </Link>
              <span aria-hidden="true" className={railPill}>
                Plexor
              </span>
            </SidebarMenuItem>

            {/* App launcher — opens the "Центр управления" sheet (all services). */}
            <SidebarMenuItem className="group/menu-item relative">
              <SidebarMenuButton
                onClick={() => setLauncherOpen(true)}
                aria-label="Все сервисы"
                className="transition-colors hover:bg-sidebar-foreground/12 hover:text-sidebar-foreground"
              >
                <SquaresFour weight="bold" />
                <span>Все сервисы</span>
              </SidebarMenuButton>
              <span aria-hidden="true" className={railPill}>
                Все сервисы
              </span>
            </SidebarMenuItem>

            {/* Search — opens the search modal (also ⌘K). */}
            <SidebarMenuItem className="group/menu-item relative">
              <SidebarMenuButton
                onClick={() => setSearchOpen(true)}
                aria-label="Поиск"
                className="transition-colors hover:bg-sidebar-foreground/12 hover:text-sidebar-foreground"
              >
                <MagnifyingGlass weight="bold" />
                <span>Поиск</span>
              </SidebarMenuButton>
              <span aria-hidden="true" className={railPill}>
                Поиск&nbsp;&nbsp;⌘K
              </span>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarHeader>

        <SidebarContent className="gap-0 group-data-[collapsible=icon]:overflow-visible">
          <SidebarSeparator className="my-1.5" />
          {navSections.map((section, i) => (
            <Fragment key={section.label}>
              {i > 0 && <SidebarSeparator className="my-1.5" />}
              <SidebarGroup className="py-0">
                <SidebarGroupContent>
                  <SidebarMenu className="gap-1">
                    {section.items.map((item) => {
                      const active = isActiveRoute(pathname, item.to);
                      const ItemIcon = item.icon;
                      return (
                        <SidebarMenuItem key={item.to}>
                          <SidebarMenuButton
                            isActive={active}
                            className={cn(
                              // Active = solid monochrome ink chip (kept off --sidebar-primary
                              // because its dark-theme value is the shadcn indigo, not monochrome).
                              'transition-colors data-active:bg-sidebar-foreground data-active:text-sidebar',
                              active
                                ? 'hover:bg-sidebar-foreground hover:text-sidebar'
                                : // Inactive hover: 12% ink wash — same family as the active chip.
                                  'hover:bg-sidebar-foreground/12 hover:text-sidebar-foreground',
                            )}
                            render={<Link to={item.to} />}
                          >
                            <ItemIcon weight={active ? 'fill' : 'bold'} />
                            <span>{item.title}</span>
                          </SidebarMenuButton>
                          <span aria-hidden="true" className={railPill}>
                            {item.title}
                          </span>
                        </SidebarMenuItem>
                      );
                    })}
                  </SidebarMenu>
                </SidebarGroupContent>
              </SidebarGroup>
            </Fragment>
          ))}
        </SidebarContent>
      </Sidebar>

      <SearchCommand open={searchOpen} onOpenChange={setSearchOpen} />
      <AppLauncher open={launcherOpen} onOpenChange={setLauncherOpen} />
    </>
  );
}
