import { useState } from 'react';
import { Link, useRouterState } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { GridView, Logout, Settings } from '@nine-thirty-five/material-symbols-react/rounded/700';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
  SidebarTrigger,
} from '@/shared/ui/primitives/sidebar';
import { Button } from '@/shared/ui/primitives/button';
import { Avatar, AvatarFallback } from '@/shared/ui/primitives/avatar';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/ui/primitives/dropdown-menu';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { toast } from 'sonner';
import type { Icon } from '@nine-thirty-five/material-symbols-react';
import {
  SECTIONS,
  isActiveRoute,
  sectionIdForPathname,
  sectionPrimaryRoute,
  type AppRoute,
} from './nav-config';
import { AppLauncher } from './app-launcher';
import { AppSettingsDialog } from './app-settings-dialog';
import { PlexorMark } from './plexor-mark';

type SidebarItem = { title: string; icon: Icon; to?: AppRoute };

/**
 * Our own rail tooltip (not the shadcn native one): a frosted label pill that
 * only exists when the rail is collapsed, fades + slides in on rail hover, and
 * the hovered item's pill nudges toward its icon.
 */
const railPill =
  'pointer-events-none absolute top-1/2 left-full z-50 ml-3.5 hidden -translate-y-1/2 translate-x-1 whitespace-nowrap rounded-md bg-foreground/70 px-2 py-1 text-xs font-medium text-background opacity-0 shadow-sm backdrop-blur-md transition-all duration-150 ease-out group-data-[collapsible=icon]:block group-hover/rail:translate-x-0 group-hover/rail:opacity-100 group-hover/menu-item:ml-2.5 group-hover/menu-item:bg-foreground/80';

/**
 * Contextual sidebar (single_contextual): shows the pages of the CURRENT
 * section. Section switching happens through the app launcher.
 * On the overview (`/`) it lists the sections themselves as entry points.
 * User lives at the bottom; its menu opens the Settings modal.
 */
export function AppSidebar() {
  const { t } = useTranslation();
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const [launcherOpen, setLauncherOpen] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);

  const section = SECTIONS.find((s) => s.id === sectionIdForPathname(pathname));

  const groupLabel = section ? t(section.label) : t('shell.applications');
  const items: SidebarItem[] = section
    ? section.pages.map((p) => ({ title: t(p.title), icon: p.icon, to: p.to }))
    : SECTIONS.map((s) => ({ title: t(s.label), icon: s.icon, to: sectionPrimaryRoute(s) }));

  return (
    <>
      {/* group/rail: hovering the collapsed rail reveals all label pills. */}
      <Sidebar collapsible="icon" data-od-id="app-sidebar" className="group/rail">
        <SidebarHeader className="gap-2 p-2">
          {/* Expanded: [P] Plexor / by ▪stbl …… [collapse]. Collapsed: just [P] (rest hidden). */}
          <div className="flex items-center gap-2">
            <Link
              to="/"
              aria-label={t('shell.goHome')}
              className="flex items-center gap-2 rounded-md p-1 text-foreground outline-none transition-opacity hover:opacity-80 focus-visible:ring-2 focus-visible:ring-sidebar-ring"
            >
              <PlexorMark className="h-6 w-auto shrink-0 group-data-[collapsible=icon]:h-5" />
              <div className="flex flex-col group-data-[collapsible=icon]:hidden">
                <span className="text-sm font-semibold tracking-tight leading-tight">
                  Plexor
                </span>
                <span className="flex items-center gap-1 font-mono text-[10px] leading-tight text-muted-foreground/70">
                  by
                  <svg
                    viewBox="0 0 64 64"
                    className="inline-block size-2.5"
                    aria-hidden="true"
                  >
                    <rect width="64" height="64" className="fill-foreground" />
                    <rect x="16" y="16" width="32" height="32" className="fill-background" />
                  </svg>
                  stbl
                </span>
              </div>
            </Link>
            <SidebarTrigger
              aria-label={t('shell.collapseMenu')}
              className="ml-auto size-7 group-data-[collapsible=icon]:hidden"
            />
          </div>
          <SidebarMenu>
            <SidebarMenuItem className="group/menu-item relative">
              <SidebarMenuButton onClick={() => setLauncherOpen(true)} className="font-medium">
                <GridView />
                <span>{t('shell.applications')}</span>
              </SidebarMenuButton>
              <span aria-hidden="true" className={railPill}>
                {t('shell.applications')}
              </span>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarHeader>

        <SidebarContent className="group-data-[collapsible=icon]:overflow-visible">
          <SidebarGroup>
            <div className="px-2 pb-1 text-[11px] font-medium tracking-[0.06em] text-muted-foreground uppercase group-data-[collapsible=icon]:hidden">
              {groupLabel}
            </div>
            <SidebarGroupContent>
              <SidebarMenu>
                {items.map((item) => {
                  const ItemIcon = item.icon;
                  const active = !!item.to && isActiveRoute(pathname, item.to);
                  if (item.to) {
                    return (
                      <SidebarMenuItem key={item.title} className="group/menu-item relative">
                        <SidebarMenuButton isActive={active} render={<Link to={item.to} />}>
                          <ItemIcon />
                          <span>{item.title}</span>
                        </SidebarMenuButton>
                        <span aria-hidden="true" className={railPill}>
                          {item.title}
                        </span>
                      </SidebarMenuItem>
                    );
                  }
                  return (
                    <SidebarMenuItem key={item.title} className="group/menu-item relative">
                      <SidebarMenuButton disabled aria-disabled className="opacity-60">
                        <ItemIcon />
                        <span>{item.title}</span>
                        <StatusPill
                          variant="idle"
                          hideDot
                          className="ml-auto px-1.5 py-0 text-[9.5px] font-normal group-data-[collapsible=icon]:hidden"
                        >
                          скоро
                        </StatusPill>
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
        </SidebarContent>

        <SidebarFooter className="p-2">
          <DropdownMenu>
            <DropdownMenuTrigger
              render={
                <Button
                  variant="ghost"
                  aria-label={t('shell.account')}
                  className="h-auto w-full justify-start gap-2 p-2 font-normal group-data-[collapsible=icon]:size-8 group-data-[collapsible=icon]:justify-center group-data-[collapsible=icon]:p-0"
                />
              }
            >
              <Avatar className="size-7">
                <AvatarFallback className="text-[10px]">{t('shell.user.initials')}</AvatarFallback>
              </Avatar>
              <span className="min-w-0 flex-1 text-left group-data-[collapsible=icon]:hidden">
                <span className="block truncate text-xs font-medium">{t('shell.user.name')}</span>
                <span className="block truncate font-mono text-[10px] text-muted-foreground">
                  {t('shell.user.email')}
                </span>
              </span>
            </DropdownMenuTrigger>
            <DropdownMenuContent side="top" align="start" className="w-56">
              <DropdownMenuGroup>
                <DropdownMenuLabel className="flex flex-col gap-0.5">
                  <span className="text-sm">{t('shell.user.name')}</span>
                  <span className="font-mono text-[11px] font-normal text-muted-foreground">
                    {t('shell.user.email')}
                  </span>
                </DropdownMenuLabel>
              </DropdownMenuGroup>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => setSettingsOpen(true)}>
                <Settings className="size-4" />
                {t('shell.userMenu.settings')}
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => toast(t('shell.userMenu.signedOut'))}>
                <Logout className="size-4" />
                {t('shell.userMenu.signOut')}
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </SidebarFooter>

        <SidebarRail />
      </Sidebar>

      <AppLauncher open={launcherOpen} onOpenChange={setLauncherOpen} />
      <AppSettingsDialog open={settingsOpen} onOpenChange={setSettingsOpen} />
    </>
  );
}
