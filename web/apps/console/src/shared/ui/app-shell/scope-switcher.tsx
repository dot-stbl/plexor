import { useState } from 'react';
import type { Icon } from '@phosphor-icons/react';
import { Buildings, Folder, CaretDown, Check } from '@phosphor-icons/react';
import { Button } from '@/shared/ui/primitives/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from '@/shared/ui/primitives/dropdown-menu';

const PROJECTS = ['plexor-core', 'plexor-staging'];
const FOLDERS = ['default', 'production'];

function ScopeDropdown({
  icon: LeadIcon,
  label,
  value,
  options,
  onSelect,
}: {
  icon: Icon;
  label: string;
  value: string;
  options: string[];
  onSelect: (value: string) => void;
}) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={<Button variant="ghost" size="sm" className="h-8 gap-1.5 px-2 font-normal" />}
      >
        <LeadIcon className="size-4 text-muted-foreground" />
        <span className="max-w-40 truncate">{value}</span>
        <CaretDown className="size-3 text-muted-foreground" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-52">
        <DropdownMenuGroup>
          <DropdownMenuLabel>{label}</DropdownMenuLabel>
          {options.map((option) => (
            <DropdownMenuItem key={option} onClick={() => onSelect(option)}>
              <Check className={option === value ? 'size-4 opacity-100' : 'size-4 opacity-0'} />
              <span>{option}</span>
            </DropdownMenuItem>
          ))}
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/** Current project + folder scope — always shown at the top of the shell. */
export function ScopeSwitcher() {
  const [project, setProject] = useState(PROJECTS[0]);
  const [folder, setFolder] = useState(FOLDERS[0]);

  return (
    <div className="flex items-center gap-0.5 text-sm" data-od-id="scope-switcher">
      <ScopeDropdown icon={Buildings} label="Проект" value={project} options={PROJECTS} onSelect={setProject} />
      <span className="text-muted-foreground/50">/</span>
      <ScopeDropdown icon={Folder} label="Папка" value={folder} options={FOLDERS} onSelect={setFolder} />
    </div>
  );
}
