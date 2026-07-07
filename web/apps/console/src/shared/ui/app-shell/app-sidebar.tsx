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
 * `open={false}`). Labels are shown as tooltips on hover.
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
            className="flex size-9 items-center justify-center rounded-md bg-sidebar-primary text-sidebar-primary-foreground outline-none transition-opacity hover:opacity-90 focus-visible:ring-2 focus-visible:ring-sidebar-ring"
          >
            <PlexorMark className="size-4" />
          </Link>
        </div>
      </SidebarHeader>

      <SidebarContent>
        {navSections.map((section) => (
          <SidebarGroup key={section.label}>
            <SidebarGroupContent>
              <SidebarMenu>
                {section.items.map((item) => {
                  const active = isActiveRoute(pathname, item.to);
                  const ItemIcon = item.icon;
                  return (
                    <SidebarMenuItem key={item.to}>
                      <SidebarMenuButton
                        isActive={active}
                        tooltip={item.title}
                        render={<Link to={item.to} />}
                      >
                        <ItemIcon weight={active ? 'fill' : 'regular'} />
                        <span>{item.title}</span>
                      </SidebarMenuButton>
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
