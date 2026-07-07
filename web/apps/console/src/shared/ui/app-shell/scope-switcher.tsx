import { useEffect, useState } from 'react';
import type { Icon } from '@phosphor-icons/react';
import { toast } from 'sonner';
import {
  Buildings,
  UsersThree,
  Folder,
  FolderOpen,
  CaretDown,
  CaretRight,
  DotsThree,
  MagnifyingGlass,
  Plus,
  PencilSimple,
  Trash,
  ArrowSquareOut,
  Copy,
} from '@phosphor-icons/react';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Label } from '@/shared/ui/primitives/label';
import { ScrollArea } from '@/shared/ui/primitives/scroll-area';
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/ui/primitives/popover';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/ui/primitives/dialog';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/shared/ui/primitives/alert-dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/shared/ui/primitives/dropdown-menu';
import { cn } from '@/lib/utils';

// Scope hierarchy: Organization → Team → Folder.
type FolderNode = { id: string; name: string };
type TeamNode = { id: string; name: string; folders: FolderNode[] };
type OrgNode = { id: string; name: string; teams: TeamNode[] };

const INITIAL_ORGS: OrgNode[] = [
  {
    id: 'org-cloudhybrid',
    name: 'cloudhybrid',
    teams: [
      {
        id: 'team-hybrid-console',
        name: 'hybrid-console',
        folders: [
          { id: 'fld-console-x', name: 'console-x' },
          { id: 'fld-console-staging', name: 'console-staging' },
        ],
      },
      {
        id: 'team-platform-core',
        name: 'platform-core',
        folders: [
          { id: 'fld-default', name: 'default' },
          { id: 'fld-sandbox', name: 'sandbox' },
        ],
      },
    ],
  },
];

const newId = (prefix: string) => `${prefix}-${crypto.randomUUID().slice(0, 8)}`;

function renameNode(orgs: OrgNode[], id: string, name: string): OrgNode[] {
  return orgs.map((org) => ({
    ...org,
    name: org.id === id ? name : org.name,
    teams: org.teams.map((team) => ({
      ...team,
      name: team.id === id ? name : team.name,
      folders: team.folders.map((folder) => (folder.id === id ? { ...folder, name } : folder)),
    })),
  }));
}

function removeNode(orgs: OrgNode[], id: string): OrgNode[] {
  return orgs
    .filter((org) => org.id !== id)
    .map((org) => ({
      ...org,
      teams: org.teams
        .filter((team) => team.id !== id)
        .map((team) => ({ ...team, folders: team.folders.filter((folder) => folder.id !== id) })),
    }));
}

type RowAction = { label: string; icon: Icon; onSelect?: () => void; destructive?: boolean };

function RowActions({ actions }: { actions: RowAction[] }) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label="Действия"
            className="shrink-0 text-muted-foreground opacity-0 group-hover:opacity-100 data-[state=open]:opacity-100"
          />
        }
      >
        <DotsThree className="size-4" weight="bold" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-44">
        {actions.map((action) => {
          const ActionIcon = action.icon;
          return (
            <DropdownMenuItem
              key={action.label}
              onClick={action.onSelect}
              className={action.destructive ? 'text-destructive' : undefined}
            >
              <ActionIcon className="size-4" />
              <span>{action.label}</span>
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function ScopeRow({
  depth,
  icon: RowIcon,
  name,
  expandable,
  open,
  active,
  onToggle,
  onSelect,
  actions,
}: {
  depth: number;
  icon: Icon;
  name: string;
  expandable?: boolean;
  open?: boolean;
  active?: boolean;
  onToggle?: () => void;
  onSelect?: () => void;
  actions: RowAction[];
}) {
  return (
    <div className={cn('group flex items-center rounded-md pr-1 hover:bg-muted', active && 'bg-muted')}>
      <button
        type="button"
        onClick={expandable ? onToggle : onSelect}
        className="flex min-w-0 flex-1 items-center gap-1.5 py-1.5 pr-1 text-left text-sm outline-none"
        style={{ paddingLeft: 8 + depth * 16 }}
      >
        {expandable ? (
          open ? (
            <CaretDown className="size-3 shrink-0 text-muted-foreground" />
          ) : (
            <CaretRight className="size-3 shrink-0 text-muted-foreground" />
          )
        ) : (
          <span className="size-3 shrink-0" />
        )}
        <RowIcon className="size-4 shrink-0 text-muted-foreground" weight={active ? 'fill' : 'regular'} />
        <span className={cn('truncate', active && 'font-medium text-foreground')}>{name}</span>
      </button>
      <RowActions actions={actions} />
    </div>
  );
}

/** Create/rename name dialog — resets its field each time it opens. */
function NameDialog({
  open,
  title,
  label,
  initial,
  submitLabel,
  onOpenChange,
  onSubmit,
}: {
  open: boolean;
  title: string;
  label: string;
  initial: string;
  submitLabel: string;
  onOpenChange: (open: boolean) => void;
  onSubmit: (name: string) => void;
}) {
  const [name, setName] = useState(initial);
  useEffect(() => {
    if (open) setName(initial);
  }, [open, initial]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={(event) => {
            event.preventDefault();
            const trimmed = name.trim();
            if (trimmed) onSubmit(trimmed);
          }}
        >
          <div className="space-y-1.5">
            <Label htmlFor="scope-name-input">{label}</Label>
            <Input
              id="scope-name-input"
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="Введите название"
              autoFocus
            />
          </div>
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" />}>Отмена</DialogClose>
            <Button type="submit" disabled={!name.trim()}>
              {submitLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/**
 * Scope selector: current team / folder chips open a management card with a
 * filter, create button, and an Organization → Team → Folder tree. Every menu
 * action is wired to local state — create, rename, delete, copy id, open.
 */
export function ScopeSwitcher() {
  const [orgs, setOrgs] = useState<OrgNode[]>(INITIAL_ORGS);
  const [open, setOpen] = useState(false);
  const [filter, setFilter] = useState('');
  const [expanded, setExpanded] = useState<Set<string>>(
    () => new Set(['org-cloudhybrid', 'team-hybrid-console']),
  );
  const [teamId, setTeamId] = useState('team-hybrid-console');
  const [folderId, setFolderId] = useState('fld-console-x');

  const [createTarget, setCreateTarget] = useState<{
    kind: 'org' | 'team' | 'folder';
    parentId?: string;
  } | null>(null);
  const [renameTarget, setRenameTarget] = useState<{ id: string; name: string } | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);

  const currentTeam = orgs.flatMap((org) => org.teams).find((team) => team.id === teamId);
  const currentFolder = currentTeam?.folders.find((folder) => folder.id === folderId);

  const q = filter.trim().toLowerCase();
  const isOpen = (id: string) => (q ? true : expanded.has(id));

  function toggle(id: string) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function selectFolder(team: TeamNode, folder: FolderNode) {
    setTeamId(team.id);
    setFolderId(folder.id);
    setOpen(false);
    setFilter('');
  }

  function handleCopy(id: string) {
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      void navigator.clipboard.writeText(id);
      toast('ID скопирован');
    }
  }

  function handleCreate(name: string) {
    if (!createTarget) return;
    const { kind, parentId } = createTarget;
    if (kind === 'org') {
      const id = newId('org');
      setOrgs((prev) => [...prev, { id, name, teams: [] }]);
      setExpanded((prev) => new Set(prev).add(id));
      toast(`Организация «${name}» создана`);
    } else if (kind === 'team' && parentId) {
      const id = newId('team');
      setOrgs((prev) =>
        prev.map((org) =>
          org.id === parentId ? { ...org, teams: [...org.teams, { id, name, folders: [] }] } : org,
        ),
      );
      setExpanded((prev) => new Set(prev).add(parentId).add(id));
      toast(`Команда «${name}» создана`);
    } else if (kind === 'folder' && parentId) {
      const id = newId('fld');
      setOrgs((prev) =>
        prev.map((org) => ({
          ...org,
          teams: org.teams.map((team) =>
            team.id === parentId ? { ...team, folders: [...team.folders, { id, name }] } : team,
          ),
        })),
      );
      setExpanded((prev) => new Set(prev).add(parentId));
      setTeamId(parentId);
      setFolderId(id);
      toast(`Папка «${name}» создана`);
    }
    setCreateTarget(null);
  }

  function handleRename(name: string) {
    if (!renameTarget) return;
    setOrgs((prev) => renameNode(prev, renameTarget.id, name));
    toast('Переименовано');
    setRenameTarget(null);
  }

  function handleDelete() {
    if (!deleteTarget) return;
    const next = removeNode(orgs, deleteTarget.id);
    setOrgs(next);
    // Keep selection valid if the active folder (or its team) was removed.
    const teams = next.flatMap((org) => org.teams);
    const team = teams.find((candidate) => candidate.id === teamId);
    if (!team || !team.folders.some((folder) => folder.id === folderId)) {
      const firstTeam = teams.find((candidate) => candidate.folders.length > 0);
      setTeamId(firstTeam?.id ?? '');
      setFolderId(firstTeam?.folders[0]?.id ?? '');
    }
    toast(`Удалено: ${deleteTarget.name}`);
    setDeleteTarget(null);
  }

  function launchDialog(action: () => void) {
    setOpen(false);
    action();
  }

  const folderMatches = (folder: FolderNode) => !q || folder.name.toLowerCase().includes(q);
  const teamVisible = (team: TeamNode) =>
    !q || team.name.toLowerCase().includes(q) || team.folders.some(folderMatches);
  const orgVisible = (org: OrgNode) =>
    !q || org.name.toLowerCase().includes(q) || org.teams.some(teamVisible);

  const visibleOrgs = orgs.filter(orgVisible);

  return (
    <>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger
          render={<Button variant="ghost" size="sm" className="h-8 gap-1.5 px-2 font-normal" />}
          data-od-id="scope-switcher"
        >
          <UsersThree className="size-4 text-muted-foreground" />
          <span className="max-w-36 truncate">{currentTeam?.name ?? '—'}</span>
          <span className="text-muted-foreground/50">/</span>
          <Folder className="size-4 text-muted-foreground" />
          <span className="max-w-36 truncate">{currentFolder?.name ?? '—'}</span>
          <CaretDown className="size-3 text-muted-foreground" />
        </PopoverTrigger>

        <PopoverContent align="start" className="w-80 p-0">
          <div className="flex items-center gap-2 border-b border-border p-2">
            <div className="relative flex-1">
              <MagnifyingGlass className="pointer-events-none absolute left-2 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={filter}
                onChange={(event) => setFilter(event.target.value)}
                placeholder="Фильтр по организациям и папкам"
                className="h-8 pl-8 text-sm"
              />
            </div>
            <Button
              variant="outline"
              size="icon"
              aria-label="Создать организацию"
              className="size-8 shrink-0"
              onClick={() => launchDialog(() => setCreateTarget({ kind: 'org' }))}
            >
              <Plus className="size-4" />
            </Button>
          </div>

          <ScrollArea className="max-h-80">
            <div className="p-1">
              {visibleOrgs.length === 0 && (
                <div className="px-3 py-8 text-center text-sm text-muted-foreground">Нет совпадений</div>
              )}

              {visibleOrgs.map((org) => (
                <div key={org.id}>
                  <ScopeRow
                    depth={0}
                    icon={Buildings}
                    name={org.name}
                    expandable
                    open={isOpen(org.id)}
                    onToggle={() => toggle(org.id)}
                    actions={[
                      {
                        label: 'Создать команду',
                        icon: Plus,
                        onSelect: () => launchDialog(() => setCreateTarget({ kind: 'team', parentId: org.id })),
                      },
                      {
                        label: 'Переименовать',
                        icon: PencilSimple,
                        onSelect: () => launchDialog(() => setRenameTarget({ id: org.id, name: org.name })),
                      },
                      {
                        label: 'Удалить',
                        icon: Trash,
                        destructive: true,
                        onSelect: () => launchDialog(() => setDeleteTarget({ id: org.id, name: org.name })),
                      },
                      { label: 'Копировать ID', icon: Copy, onSelect: () => handleCopy(org.id) },
                    ]}
                  />

                  {isOpen(org.id) &&
                    org.teams.filter(teamVisible).map((team) => (
                      <div key={team.id}>
                        <ScopeRow
                          depth={1}
                          icon={UsersThree}
                          name={team.name}
                          expandable
                          open={isOpen(team.id)}
                          onToggle={() => toggle(team.id)}
                          actions={[
                            {
                              label: 'Создать папку',
                              icon: Plus,
                              onSelect: () =>
                                launchDialog(() => setCreateTarget({ kind: 'folder', parentId: team.id })),
                            },
                            {
                              label: 'Переименовать',
                              icon: PencilSimple,
                              onSelect: () =>
                                launchDialog(() => setRenameTarget({ id: team.id, name: team.name })),
                            },
                            {
                              label: 'Удалить',
                              icon: Trash,
                              destructive: true,
                              onSelect: () =>
                                launchDialog(() => setDeleteTarget({ id: team.id, name: team.name })),
                            },
                            { label: 'Копировать ID', icon: Copy, onSelect: () => handleCopy(team.id) },
                          ]}
                        />

                        {isOpen(team.id) &&
                          team.folders.filter(folderMatches).map((folder) => (
                            <ScopeRow
                              key={folder.id}
                              depth={2}
                              icon={folder.id === folderId ? FolderOpen : Folder}
                              name={folder.name}
                              active={folder.id === folderId}
                              onSelect={() => selectFolder(team, folder)}
                              actions={[
                                {
                                  label: 'Открыть',
                                  icon: ArrowSquareOut,
                                  onSelect: () => selectFolder(team, folder),
                                },
                                {
                                  label: 'Переименовать',
                                  icon: PencilSimple,
                                  onSelect: () =>
                                    launchDialog(() => setRenameTarget({ id: folder.id, name: folder.name })),
                                },
                                {
                                  label: 'Удалить',
                                  icon: Trash,
                                  destructive: true,
                                  onSelect: () =>
                                    launchDialog(() => setDeleteTarget({ id: folder.id, name: folder.name })),
                                },
                                { label: 'Копировать ID', icon: Copy, onSelect: () => handleCopy(folder.id) },
                              ]}
                            />
                          ))}
                      </div>
                    ))}
                </div>
              ))}
            </div>
          </ScrollArea>
        </PopoverContent>
      </Popover>

      <NameDialog
        open={createTarget !== null}
        title={
          createTarget?.kind === 'org'
            ? 'Новая организация'
            : createTarget?.kind === 'team'
              ? 'Новая команда'
              : 'Новая папка'
        }
        label={
          createTarget?.kind === 'org'
            ? 'Название организации'
            : createTarget?.kind === 'team'
              ? 'Название команды'
              : 'Название папки'
        }
        initial=""
        submitLabel="Создать"
        onOpenChange={(next) => !next && setCreateTarget(null)}
        onSubmit={handleCreate}
      />

      <NameDialog
        open={renameTarget !== null}
        title="Переименовать"
        label="Новое название"
        initial={renameTarget?.name ?? ''}
        submitLabel="Сохранить"
        onOpenChange={(next) => !next && setRenameTarget(null)}
        onSubmit={handleRename}
      />

      <AlertDialog open={deleteTarget !== null} onOpenChange={(next) => !next && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Удалить «{deleteTarget?.name}»?</AlertDialogTitle>
            <AlertDialogDescription>
              Действие необратимо. Вложенные ресурсы также будут удалены.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Отмена</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete}>Удалить</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
