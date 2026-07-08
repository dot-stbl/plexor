import type { Vm } from '@/shared/api';
import { Button } from '@/shared/ui/primitives/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/ui/primitives/dropdown-menu';
import { DotsThree, Play, Stop, ArrowsClockwise, Terminal, Trash } from '@phosphor-icons/react';
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
  const running = vm.status === 'running';
  const stopped = vm.status === 'stopped';

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label={`Действия с ${vm.name}`}
            className="text-muted-foreground"
          />
        }
      >
        <DotsThree weight="bold" className="size-4" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-44">
        <DropdownMenuItem disabled={!stopped} onClick={() => toast(`Запустить: ${vm.name}`)}>
          <Play />
          Запустить
        </DropdownMenuItem>
        <DropdownMenuItem disabled={!running} onClick={() => toast(`Остановить: ${vm.name}`)}>
          <Stop />
          Остановить
        </DropdownMenuItem>
        <DropdownMenuItem
          disabled={!running}
          onClick={() => toast(`Перезагрузить: ${vm.name}`)}
        >
          <ArrowsClockwise />
          Перезагрузить
        </DropdownMenuItem>
        <DropdownMenuItem disabled>
          <Terminal />
          Открыть консоль
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          variant="destructive"
          onClick={() => toast(`Удалить: ${vm.name}`)}
        >
          <Trash />
          Удалить
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}