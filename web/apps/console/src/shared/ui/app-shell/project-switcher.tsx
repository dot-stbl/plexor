import { useState } from 'react';
import { CaretUpDown, Check, Plus } from '@phosphor-icons/react';
import { Button } from '@/shared/ui/primitives/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/ui/primitives/dropdown-menu';

const PROJECTS = ['plexor-core', 'plexor-staging'];

/**
 * Active-project switcher — the functional element in the sidebar header next
 * to the logo. Hidden when the sidebar collapses to icon rail.
 */
export function ProjectSwitcher() {
  const [project, setProject] = useState<string>(PROJECTS[0]);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button
            variant="ghost"
            size="sm"
            className="h-9 min-w-0 flex-1 justify-between gap-2 px-2 group-data-[collapsible=icon]:hidden"
          />
        }
      >
        <span className="grid min-w-0 text-left leading-tight">
          <span className="font-mono text-[10px] uppercase tracking-[0.08em] text-muted-foreground">
            Проект
          </span>
          <span className="truncate text-sm font-medium">{project}</span>
        </span>
        <CaretUpDown className="size-3.5 shrink-0 text-muted-foreground" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-56">
        <DropdownMenuLabel>Проекты</DropdownMenuLabel>
        {PROJECTS.map((name) => (
          <DropdownMenuItem key={name} onClick={() => setProject(name)}>
            <Check className={name === project ? 'size-4 opacity-100' : 'size-4 opacity-0'} />
            <span>{name}</span>
          </DropdownMenuItem>
        ))}
        <DropdownMenuSeparator />
        <DropdownMenuItem>
          <Plus className="size-4" />
          <span>Создать проект…</span>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
