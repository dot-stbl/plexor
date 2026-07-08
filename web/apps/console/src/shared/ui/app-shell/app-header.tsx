import { Fragment } from 'react';
import { Link, useRouterState } from '@tanstack/react-router';
import { House } from '@phosphor-icons/react';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/shared/ui/primitives/breadcrumb';
import { ScopeSwitcher } from './scope-switcher';
import { SECTIONS, isActiveRoute, sectionIdForPathname } from './nav-config';

/**
 * Slim top bar (replaces the old navbar): org/folder scope switcher on the
 * left, then breadcrumbs derived from the current route. The breadcrumbs lead
 * with a Home button → overview. Notifications, theme and account moved into
 * the sidebar / settings modal.
 */
export function AppHeader() {
  const pathname = useRouterState({ select: (state) => state.location.pathname });

  const section = SECTIONS.find((s) => s.id === sectionIdForPathname(pathname));
  const page = section?.pages.find((p) => p.to && isActiveRoute(pathname, p.to));
  const crumbs: string[] =
    pathname === '/' ? [] : [section?.label, page?.title].filter((c): c is string => !!c);

  return (
    <header
      data-od-id="app-topbar"
      className="sticky top-0 z-10 flex h-11 shrink-0 items-center gap-2.5 border-b border-border bg-background px-3"
    >
      <ScopeSwitcher />
      <span aria-hidden="true" className="h-4 w-px bg-border" />
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <Link
              to="/"
              aria-label="На главную"
              data-od-id="breadcrumb-home"
              className="flex items-center rounded-sm text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring/40"
            >
              <House className="size-3.5" weight={pathname === '/' ? 'fill' : 'bold'} />
            </Link>
          </BreadcrumbItem>
          {crumbs.map((crumb, index) => (
            <Fragment key={crumb}>
              <BreadcrumbSeparator />
              <BreadcrumbItem>
                {index === crumbs.length - 1 ? (
                  <BreadcrumbPage>{crumb}</BreadcrumbPage>
                ) : (
                  <span className="text-muted-foreground">{crumb}</span>
                )}
              </BreadcrumbItem>
            </Fragment>
          ))}
        </BreadcrumbList>
      </Breadcrumb>
    </header>
  );
}
