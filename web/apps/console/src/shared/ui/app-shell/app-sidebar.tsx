import { Link, useRouterState } from '@tanstack/react-router';
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from '@/shared/ui/primitives/sidebar';
import { PlexorMark } from './plexor-mark';
import { navSections, isActiveRoute } from './nav-config';

/**
 * Permanently-collapsed icon rail (non-expandable — see AppShell's controlled
 * `open={false}`). Hovering the rail reveals a label pill next to every icon
 * (slide + fade in); the hovered item's pill nudges toward its button.
 */
export function AppSidebar() {
  const pathname = useRouterState({ select: (state) => state.location.pathname });

  return (
    <Sidebar collapsible="icon" data-od-id="app-sidebar">
      <SidebarHeader>
        <div className="flex justify-center py-1">
          <Link
            to="/"
            aria-label="Plexor — на главную"
            className="flex h-7 w-9 items-center justify-center rounded-md bg-sidebar-primary text-sidebar-primary-foreground outline-none transition-opacity hover:opacity-90 focus-visible:ring-2 focus-visible:ring-sidebar-ring"
          >
            <PlexorMark className="h-4 w-6" />
          </Link>
        </div>
      </SidebarHeader>

      {/* group/rail: hovering anywhere in the rail reveals all label pills. */}
      <SidebarContent className="group/rail group-data-[collapsible=icon]:overflow-visible">
        {navSections.map((section) => (
          <SidebarGroup key={section.label}>
            <SidebarGroupContent>
              <SidebarMenu>
                {section.items.map((item) => {
                  const active = isActiveRoute(pathname, item.to);
                  const ItemIcon = item.icon;
                  return (
                    <SidebarMenuItem key={item.to}>
                      <SidebarMenuButton isActive={active} render={<Link to={item.to} />}>
                        <ItemIcon weight={active ? 'fill' : 'bold'} />
                        <span>{item.title}</span>
                      </SidebarMenuButton>
                      <span
                        aria-hidden="true"
                        className="pointer-events-none absolute left-full top-1/2 z-50 ml-3 -translate-y-1/2 translate-x-1 whitespace-nowrap rounded-md bg-foreground/90 px-2 py-1 text-xs font-medium text-background opacity-0 shadow-md transition-all duration-150 ease-out group-hover/rail:translate-x-0 group-hover/rail:opacity-100 group-hover/menu-item:ml-2 group-hover/menu-item:bg-foreground"
                      >
                        {item.title}
                      </span>
                    </SidebarMenuItem>
                  );
                })}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        ))}
      </SidebarContent>
    </Sidebar>
  );
}
