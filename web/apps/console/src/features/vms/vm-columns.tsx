import type { TFunction } from 'i18next';
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
export function getVmColumns(t: TFunction): ColumnDef<Vm>[] {
  return [
    {
      id: 'name',
      header: t('table.name'),
      accessorKey: 'name',
      cell: ({ row }) => <span className="font-medium text-foreground">{row.original.name}</span>,
      meta: {
        filter: { type: 'text', param: 'q', placeholder: t('table.filter.searchName') },
      },
    },
    {
      id: 'id',
      header: t('table.id'),
      accessorKey: 'id',
      cell: ({ row }) => (
        <CopyableText value={row.original.id} copyLabel={t('table.copy.id')}>
          {row.original.id}
        </CopyableText>
      ),
      meta: { size: 'w-[160px]' },
    },
    {
      id: 'status',
      header: t('table.status'),
      accessorKey: 'status',
      cell: ({ row }) => (
        <StatusPill variant={mapVmStatusToVariant(row.original.status)} size="sm">
          {row.original.status}
        </StatusPill>
      ),
      meta: {
        size: 'w-[110px]',
        filter: { type: 'select', param: 'status', options: STATUS_FILTER_OPTIONS, placeholder: t('table.filter.status') },
      },
    },
    {
      id: 'internalIp',
      header: t('table.ip'),
      accessorKey: 'internalIp',
      cell: ({ row }) => (
        <CopyableText value={row.original.internalIp} copyLabel={t('table.copy.ip')}>
          {row.original.internalIp}
        </CopyableText>
      ),
      meta: { size: 'w-[140px]' },
    },
    {
      id: 'zone',
      header: t('table.zone'),
      accessorKey: 'zone',
      cell: ({ row }) => (
        <CopyableText value={row.original.zone} copyLabel={t('table.copy.zone')}>
          {row.original.zone}
        </CopyableText>
      ),
      meta: {
        size: 'w-[110px]',
        // `eu-central-1` — technical example zone, kept literal (not localized).
        filter: { type: 'text', param: 'zone', placeholder: 'eu-central-1' },
      },
    },
    {
      id: 'flavor',
      header: t('table.flavor'),
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
      header: t('table.disk'),
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
}