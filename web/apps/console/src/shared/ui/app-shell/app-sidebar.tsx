import { useState } from 'react';
import { Link, useNavigate, useRouterState } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { Logout, Settings } from '@nine-thirty-five/material-symbols-react/rounded/700';
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
import { getBootConfig } from '@/shared/lib/config';
import { useFeatureFlag } from '@/shared/lib/feature-flags/feature-flag-context';
import { readSession } from '@/shared/lib/session';
import type { LauncherSummaryCard } from '@/mocks/launcher-summary';
import {
  SECTIONS,
  isActiveRoute,
  sectionIdForPathname,
  sectionPrimaryRoute,
  type AppRoute,
} from './nav-config';
import { AppLauncher } from './app-launcher';
import { PlexorMark, StblMark } from '@plexor/ui/brand';

type SidebarItem = { title: string; icon: Icon; to?: AppRoute };

/**
 * Our own rail tooltip (not the shadcn native one): a frosted label pill that
 * only exists when the rail is collapsed, fades + slides in on rail hover, and
 * the hovered item's pill nudges toward its icon.
 */
const railPill =
  'pointer-events-none absolute top-1/2 left-full z-tooltip ml-3.5 hidden -translate-y-1/2 translate-x-0 whitespace-nowrap rounded-md bg-foreground/70 px-2 py-1 text-xs font-medium text-background opacity-0 shadow-sm backdrop-blur-md transition-all duration-150 ease-out group-data-[collapsible=icon]:block group-hover/rail:translate-x-0 group-hover/rail:opacity-100 group-hover/menu-item:ml-2.5 group-hover/menu-item:bg-foreground/80';

/**
 * Contextual sidebar (single_contextual): shows the pages of the CURRENT
 * section. Section switching happens through the app launcher.
 * On the overview (`/`) it lists the sections themselves as entry points.
 * User lives at the bottom; its menu opens the Settings modal.
 *
 * Sections are gated by their corresponding `sidebar.show*` feature
 * flag. Hook order is stable — we call each flag once per render in a
 * fixed order, so React's rules of hooks stay satisfied even when the
 * flag map grows in future commits.
 *
 * `billing` doesn't have a shipping section in nav-config yet — the
 * flag is the contract, ready for when the section lands.
 *
 * `launcherSummary` is a pass-through prop for the launcher's SUMMARY
 * row — see `AppShell`'s doc comment for why this isn't fetched here.
 */
export function AppSidebar({
  launcherSummary = [],
}: {
  launcherSummary?: readonly LauncherSummaryCard[];
} = {}) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const [launcherOpen, setLauncherOpen] = useState(false);

  // Operator-controlled branding (TOML → window.__PLEXOR_CONFIG__ → here).
  // Falls back to the Plexor defaults when the host hasn't shipped a
  // boot config (vite dev, static export, etc).
  const { brand } = getBootConfig();
  const hasCustomLogo = brand.logoUrl !== null && brand.logoUrl !== '';

  // User identity for the footer chip + dropdown label.
  // readSession() is a hook-free localStorage read; for the sidebar it's
  // fine — the chip text only updates on a full reload (the session is
  // written by /login, and the sidebar re-mounts on route change anyway).
  const session = readSession();
  const user = session?.user ?? null;
  const displayName = user?.displayName ?? t('shell.user.name');
  const displayEmail = user?.email ?? t('shell.user.email');
  // Optional avatar URL — present only when the backend's user record
  // ships one. The Avatar primitive renders the <img> when src is set and
  // falls back to initials derived from `name` otherwise.
  const avatarSrc = user?.avatarUrl;

  // Read each section's flag in SECTIONS order — the matching index
  // is what the visibility filter later consults. The hooks fire
  // unconditionally every render in the same order, so the rules
  // of hooks hold even when the flag count changes between renders
  // (new flags added in future commits just land at the tail).
  const showNetwork = useFeatureFlag('sidebar.showNetworkSection');
  const showStorage = useFeatureFlag('sidebar.showStorageSection');
  const showObservability = useFeatureFlag('sidebar.showObservability');
  const showAdmin = useFeatureFlag('sidebar.showAdminSection');
  const sectionFlagById: Readonly<Record<string, boolean>> = {
    network: showNetwork,
    storage: showStorage,
    observability: showObservability,
    admin: showAdmin,
  };

  const visibleSections = SECTIONS.filter((s) => sectionFlagById[s.id] !== false);

  const section = visibleSections.find((s) => s.id === sectionIdForPathname(pathname));

  const groupLabel = section ? t(section.label) : t('shell.applications');
  const items: SidebarItem[] = section
    ? section.pages.map((p) => ({ title: t(p.title), icon: p.icon, to: p.to }))
    : visibleSections.map((s) => ({ title: t(s.label), icon: s.icon, to: sectionPrimaryRoute(s) }));

  return (
    <>
      {/* group/rail: hovering the collapsed rail reveals all label pills. */}
      <Sidebar collapsible="icon" data-od-id="app-sidebar" className="group/rail">
        <SidebarHeader className="gap-2 p-2">
          {/* Expanded: [logo] brand.name / by ▪stbl …… [collapse]. Collapsed: just [logo] (rest hidden). */}
          <div className="flex items-center gap-2">
            <Link
              to="/"
              aria-label={t('shell.goHome')}
              className="flex items-center gap-2 rounded-md p-1 text-foreground outline-none transition-opacity hover:opacity-80 focus-visible:ring-2 focus-visible:ring-sidebar-ring"
            >
              {hasCustomLogo ? (
                <img
                  src={brand.logoUrl ?? undefined}
                  alt={brand.name}
                  className="h-6 w-auto shrink-0 group-data-[collapsible=icon]:h-5"
                />
              ) : (
                <PlexorMark className="h-6 w-auto shrink-0 group-data-[collapsible=icon]:h-5" />
              )}
              <div className="flex flex-col group-data-[collapsible=icon]:hidden">
                <span className="text-sm font-semibold tracking-tight leading-tight">
                  {brand.name}
                </span>
                <span className="flex items-center gap-1 font-mono text-[10px] leading-tight text-muted-foreground/70">
                  by
                  <StblMark className="size-2.5" />
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
                <StblMark className="size-4" />
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
                          {t('common.soon')}
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
          <SidebarMenu>
            <SidebarMenuItem className="group/menu-item relative">
              <SidebarMenuButton
                isActive={isActiveRoute(pathname, '/settings/profile')}
                render={<Link to="/settings/profile" data-testid="sidebar-settings-link" />}
              >
                <Settings />
                <span>{t('shell.userMenu.settings')}</span>
              </SidebarMenuButton>
              <span aria-hidden="true" className={railPill}>
                {t('shell.userMenu.settings')}
              </span>
            </SidebarMenuItem>
          </SidebarMenu>

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
              {user ? (
                <Avatar
                  className="size-7"
                  name={displayName}
                  src={avatarSrc}
                />
              ) : (
                <Avatar className="size-7">
                  <AvatarFallback className="text-[10px]">{t('shell.user.initials')}</AvatarFallback>
                </Avatar>
              )}
              <span className="min-w-0 flex-1 text-left group-data-[collapsible=icon]:hidden">
                <span className="block truncate text-xs font-medium">{displayName}</span>
                <span className="block truncate font-mono text-[10px] text-muted-foreground">
                  {displayEmail}
                </span>
              </span>
            </DropdownMenuTrigger>
            <DropdownMenuContent side="top" align="start" className="w-56">
              <DropdownMenuGroup>
                <DropdownMenuLabel className="flex flex-col gap-0.5">
                  <span className="text-sm">{displayName}</span>
                  <span className="font-mono text-[11px] font-normal text-muted-foreground">
                    {displayEmail}
                  </span>
                </DropdownMenuLabel>
              </DropdownMenuGroup>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                onClick={() => {
                  void navigate({ to: '/settings/profile' });
                }}
              >
                <Settings className="size-4" />
                {t('shell.userMenu.settings')}
              </DropdownMenuItem>
              <DropdownMenuItem
                onClick={() => {
                  toast(t('shell.userMenu.signedOut'));
                }}
              >
                <Logout className="size-4" />
                {t('shell.userMenu.signOut')}
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </SidebarFooter>

        <SidebarRail />
      </Sidebar>

      <AppLauncher open={launcherOpen} onOpenChange={setLauncherOpen} summary={launcherSummary} />
    </>
  );
}