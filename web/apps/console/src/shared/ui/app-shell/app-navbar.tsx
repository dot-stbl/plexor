import { Bell } from '@phosphor-icons/react';
import { Button } from '@/shared/ui/primitives/button';
import { ModeToggle } from '@/shared/ui/primitives/theme-toggle';
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
import { ScopeSwitcher } from './scope-switcher';

/** Persistent top bar: current project/folder scope (left) + global utilities. */
export function AppNavbar() {
  return (
    <header
      data-od-id="app-topbar"
      className="sticky top-0 z-10 flex h-12 shrink-0 items-center gap-2 border-b border-border bg-background/80 px-3 backdrop-blur-sm"
    >
      <ScopeSwitcher />

      <div className="ml-auto flex items-center gap-1.5">
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
            <DropdownMenuGroup>
              <DropdownMenuLabel className="flex flex-col gap-0.5">
                <span className="text-sm">Алексей Сергеев</span>
                <span className="font-mono text-[11px] font-normal text-muted-foreground">
                  a.sergeev@hybrid.ai
                </span>
              </DropdownMenuLabel>
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuItem>Настройки проекта</DropdownMenuItem>
            <DropdownMenuItem>Выйти</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
