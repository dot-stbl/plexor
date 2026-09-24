import { useTranslation } from 'react-i18next';
import type { Vm } from '@/shared/api';
import { Button } from '@/shared/ui/primitives/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/ui/primitives/dropdown-menu';
import {
  Delete,
  MoreHoriz,
  PlayArrow,
  Stop,
  Sync,
  Terminal
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import { toast } from 'sonner';

interface VmRowActionsProps {
  vm: Vm;
}

/**
 * Per-row overflow menu. Disabled state is derived from the VM's status
 * (you can't stop a stopped VM) — no extra props needed. Actions are
 * toast-only stubs in MVP; real mutations land with the wiring story.
 */
export function VmRowActions({ vm }: VmRowActionsProps) {
  const { t } = useTranslation();
  const running = vm.status === 'running';
  const stopped = vm.status === 'stopped';

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label={t('vms.rowActions.ariaLabel', { name: vm.name })}
            className="text-muted-foreground"
          />
        }
      >
        <MoreHoriz className="size-4" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-44">
        <DropdownMenuItem
          disabled={!stopped}
          onClick={() => toast(t('vms.rowActions.startToast', { name: vm.name }))}
        >
          <PlayArrow />
          {t('vms.rowActions.start')}
        </DropdownMenuItem>
        <DropdownMenuItem
          disabled={!running}
          onClick={() => toast(t('vms.rowActions.stopToast', { name: vm.name }))}
        >
          <Stop />
          {t('vms.rowActions.stop')}
        </DropdownMenuItem>
        <DropdownMenuItem
          disabled={!running}
          onClick={() => toast(t('vms.rowActions.restartToast', { name: vm.name }))}
        >
          <Sync />
          {t('vms.rowActions.restart')}
        </DropdownMenuItem>
        <DropdownMenuItem disabled>
          <Terminal />
          {t('vms.rowActions.openConsole')}
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          variant="destructive"
          onClick={() => toast(t('vms.rowActions.deleteToast', { name: vm.name }))}
        >
          <Delete />
          {t('vms.rowActions.delete')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}