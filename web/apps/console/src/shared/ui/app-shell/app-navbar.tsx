import { Link, useRouterState } from '@tanstack/react-router';
import { MagnifyingGlass, Bell } from '@phosphor-icons/react';
import { SidebarTrigger } from '@/shared/ui/primitives/sidebar';
import { Separator } from '@/shared/ui/primitives/separator';
import { Button } from '@/shared/ui/primitives/button';
import { ModeToggle } from '@/shared/ui/primitives/theme-toggle';
import { Avatar, AvatarFallback } from '@/shared/ui/primitives/avatar';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/shared/ui/primitives/breadcrumb';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/ui/primitives/dropdown-menu';
import { navItems, isActiveRoute } from './nav-config';

export function AppNavbar({ onOpenSearch }: { onOpenSearch: () => void }) {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const current = navItems.find((item) => isActiveRoute(pathname, item.to)) ?? navItems[0];

  return (
    <header
      data-od-id="app-navbar"
      className="sticky top-0 z-10 flex h-14 shrink-0 items-center gap-2 border-b border-border bg-background/80 px-3 backdrop-blur-sm"
    >
      <SidebarTrigger className="text-muted-foreground" />
      <Separator orientation="vertical" className="mr-1 h-5" />

      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem className="hidden sm:block">
            <BreadcrumbLink render={<Link to="/" />}>Plexor</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator className="hidden sm:block" />
          <BreadcrumbItem>
            <BreadcrumbPage>{current.title}</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <div className="ml-auto flex items-center gap-1.5">
        <Button
          variant="outline"
          size="sm"
          onClick={onOpenSearch}
          className="hidden gap-2 text-muted-foreground sm:inline-flex"
        >
          <MagnifyingGlass className="size-4" />
          <span className="text-xs">Поиск…</span>
          <kbd className="ml-1 rounded border border-border bg-muted px-1.5 font-mono text-[10px] leading-4">
            ⌘K
          </kbd>
        </Button>
        <Button
          variant="ghost"
          size="icon"
          onClick={onOpenSearch}
          aria-label="Поиск"
          className="sm:hidden"
        >
          <MagnifyingGlass className="size-4" />
        </Button>

        <Button variant="ghost" size="icon" aria-label="Уведомления">
          <Bell className="size-4" />
        </Button>

        <ModeToggle />

        <DropdownMenu>
          <DropdownMenuTrigger
            render={<Button variant="ghost" size="icon" aria-label="Аккаунт" className="rounded-full" />}
          >
            <Avatar className="size-6">
              <AvatarFallback className="text-[10px]">АС</AvatarFallback>
            </Avatar>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-52">
            <DropdownMenuLabel className="flex flex-col gap-0.5">
              <span className="text-sm">Алексей Сергеев</span>
              <span className="font-mono text-[11px] font-normal text-muted-foreground">
                a.sergeev@hybrid.ai
              </span>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem>Настройки проекта</DropdownMenuItem>
            <DropdownMenuItem>Выйти</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
