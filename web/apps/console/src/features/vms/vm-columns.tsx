import type { Vm, VmStatus } from '@/shared/api';
import type { ColumnDef } from '@/shared/ui/data-table';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { CopyableText } from '@/shared/ui/primitives/copyable-text';
import { mapVmStatusToVariant } from './vm-status';
import { VmRowActions } from './vm-row-actions';

const STATUS_FILTER_OPTIONS: { value: VmStatus; label: string }[] = [
  { value: 'running', label: 'Running' },
  { value: 'stopped', label: 'Stopped' },
  { value: 'error', label: 'Error' },
  { value: 'provisioning', label: 'Provisioning' },
  { value: 'idle', label: 'Idle' },
];

/**
 * Single source of truth for the VM list columns. Each column declares:
 *   - how to render its cell
 *   - how to filter (`meta.filter.type` + `param` = API query-param key)
 *
 * The screen maps `FilterValues` → `ListVmsQueryParams` 1:1; column param
 * keys (`status`, `zone`, `q`) match the kubb-generated query-params type.
 */
export const vmColumns: ColumnDef<Vm>[] = [
  {
    id: 'name',
    header: 'Имя',
    accessorKey: 'name',
    cell: ({ row }) => (
      <div className="flex flex-col gap-0.5">
        <span className="font-medium text-foreground">{row.original.name}</span>
        <CopyableText
          value={row.original.id}
          copyLabel="Скопировать ID"
          className="text-[10px]"
        >
          {row.original.id}
        </CopyableText>
      </div>
    ),
    meta: {
      filter: { type: 'text', param: 'q', placeholder: 'Поиск по имени, IP или ID' },
    },
  },
  {
    id: 'status',
    header: 'Статус',
    accessorKey: 'status',
    cell: ({ row }) => (
      <StatusPill variant={mapVmStatusToVariant(row.original.status)} size="sm">
        {row.original.status}
      </StatusPill>
    ),
    meta: {
      size: 'w-[110px]',
      filter: { type: 'select', param: 'status', options: STATUS_FILTER_OPTIONS, placeholder: 'Все статусы' },
    },
  },
  {
    id: 'internalIp',
    header: 'IP',
    accessorKey: 'internalIp',
    cell: ({ row }) => (
      <CopyableText value={row.original.internalIp} copyLabel="Скопировать IP">
        {row.original.internalIp}
      </CopyableText>
    ),
    meta: { size: 'w-[140px]' },
  },
  {
    id: 'zone',
    header: 'Зона',
    accessorKey: 'zone',
    cell: ({ row }) => (
      <CopyableText value={row.original.zone} copyLabel="Скопировать зону">
        {row.original.zone}
      </CopyableText>
    ),
    meta: {
      size: 'w-[110px]',
      filter: { type: 'text', param: 'zone', placeholder: 'eu-central-1' },
    },
  },
  {
    id: 'flavor',
    header: 'Флейвор',
    accessorFn: (row) => `${row.vcpu} vCPU · ${row.ramGb} GB`,
    cell: ({ getValue }) => (
      <span className="text-muted-foreground">
        <MonoNum>{String(getValue()).split(' ')[0]}</MonoNum> vCPU ·{' '}
        <MonoNum>{String(getValue()).split(' ')[2]}</MonoNum> GB
      </span>
    ),
    meta: { size: 'w-[130px]' },
  },
  {
    id: 'diskGb',
    header: 'Диск',
    accessorKey: 'diskGb',
    cell: ({ getValue }) => (
      <>
        <MonoNum muted>{getValue<number>()}</MonoNum>
        <span className="ml-0.5 text-muted-foreground">GB</span>
      </>
    ),
    meta: { size: 'w-[70px]', align: 'right' },
  },
  {
    id: 'actions',
    header: '',
    accessorKey: 'id',
    enableSorting: false,
    enableColumnFilter: false,
    cell: ({ row }) => <VmRowActions vm={row.original} />,
    meta: { size: 'w-9' },
  },
];