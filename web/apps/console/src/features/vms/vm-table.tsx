import type { Vm } from '@/shared/api';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/shared/ui/primitives/table';
import { Checkbox } from '@/shared/ui/primitives/checkbox';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { IP } from '@/shared/ui/primitives/ip';
import { mapVmStatusToVariant } from './vm-status';
import { VmRowActions } from './vm-row-actions';

interface VmTableProps {
  items: readonly Vm[];
  selectedIds: ReadonlySet<string>;
  onToggle: (id: string) => void;
  onToggleAll: (next: boolean) => void;
  onRowClick: (vm: Vm) => void;
}

/**
 * Plexor VM list table — 8 columns, compact rows. Pure presentation;
 * selection state lives in the parent. Built on shadcn `Table` primitives
 * so we get row hover, selection styling and density for free.
 */
export function VmTable({ items, selectedIds, onToggle, onToggleAll, onRowClick }: VmTableProps) {
  const allSelected = items.length > 0 && items.every((vm) => selectedIds.has(vm.id));
  const partialSelected = !allSelected && items.some((vm) => selectedIds.has(vm.id));

  return (
    <Table data-od-id="vms-table">
      <TableHeader>
        <TableRow>
          <TableHead className="w-9">
            <Checkbox
              checked={allSelected}
              onCheckedChange={(value) => onToggleAll(value === true)}
              aria-label="Выбрать все"
              {...(partialSelected && { 'data-indeterminate': '' })}
            />
          </TableHead>
          <TableHead>Имя</TableHead>
          <TableHead className="w-[110px]">Статус</TableHead>
          <TableHead className="w-[130px]">IP</TableHead>
          <TableHead className="w-[100px]">Зона</TableHead>
          <TableHead className="w-[120px]">Флейвор</TableHead>
          <TableHead className="w-[80px] text-right">Диск</TableHead>
          <TableHead className="w-9" />
        </TableRow>
      </TableHeader>
      <TableBody>
        {items.map((vm) => {
          const isSelected = selectedIds.has(vm.id);
          return (
            <TableRow
              key={vm.id}
              data-state={isSelected ? 'selected' : undefined}
              onClick={() => onRowClick(vm)}
              className="cursor-pointer"
            >
              <TableCell onClick={(e) => e.stopPropagation()}>
                <Checkbox
                  checked={isSelected}
                  onCheckedChange={() => onToggle(vm.id)}
                  aria-label={`Выбрать ${vm.name}`}
                />
              </TableCell>
              <TableCell>
                <div className="flex flex-col">
                  <span className="font-medium text-foreground">{vm.name}</span>
                  <MonoNum muted className="text-[10px]">
                    {vm.id}
                  </MonoNum>
                </div>
              </TableCell>
              <TableCell>
                <StatusPill variant={mapVmStatusToVariant(vm.status)} size="sm">
                  {vm.status}
                </StatusPill>
              </TableCell>
              <TableCell>
                <IP value={vm.internalIp} />
              </TableCell>
              <TableCell>
                <MonoNum muted>{vm.zone}</MonoNum>
              </TableCell>
              <TableCell>
                <span className="text-muted-foreground">
                  <MonoNum>{vm.vcpu}</MonoNum> vCPU · <MonoNum>{vm.ramGb}</MonoNum> GB
                </span>
              </TableCell>
              <TableCell className="text-right">
                <MonoNum muted>{vm.diskGb}</MonoNum>
                <span className="ml-0.5 text-muted-foreground">GB</span>
              </TableCell>
              <TableCell onClick={(e) => e.stopPropagation()}>
                <VmRowActions vm={vm} />
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}