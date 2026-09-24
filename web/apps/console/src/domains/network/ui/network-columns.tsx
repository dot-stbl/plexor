import type { TFunction } from 'i18next';
import type { ColumnDef } from '@/shared/ui/data-table';
import type { Network } from '@/shared/api/mocks/handmade/networks';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { StatusPill } from '@/shared/ui/primitives/status-pill';

/** Колонки таблицы VPC: имя, CIDR, регион, подсети, привязки, статус. */
export function getNetworkColumns(t: TFunction): ColumnDef<Network>[] {
  return [
    {
      id: 'name',
      header: t('table.name'),
      accessorKey: 'name',
      cell: ({ row }) => (
        <span className="flex flex-col">
          <span className="font-medium text-foreground">{row.original.name}</span>
          <span className="font-mono text-[10.5px] text-muted-foreground">{row.original.region}</span>
        </span>
      ),
    },
    {
      id: 'cidr',
      header: t('networks.table.cidr'),
      accessorKey: 'cidr',
      cell: ({ getValue }) => (
        <span className="font-mono text-xs text-muted-foreground">{getValue<string>()}</span>
      ),
    },
    {
      id: 'subnets',
      header: t('networks.table.subnets'),
      accessorKey: 'subnetCount',
      cell: ({ getValue }) => <MonoNum muted>{getValue<number>()}</MonoNum>,
      meta: { size: 'w-[90px]', align: 'right' },
    },
    {
      id: 'bindings',
      header: t('networks.table.bindings'),
      accessorKey: 'bindingCount',
      cell: ({ getValue }) => <MonoNum muted>{getValue<number>()}</MonoNum>,
      meta: { size: 'w-[100px]', align: 'right' },
    },
    {
      id: 'status',
      header: t('networks.table.status'),
      accessorKey: 'status',
      cell: ({ row }) => {
        const variant = row.original.status === 'active' ? 'ok' : 'pending';
        const labelKey =
          row.original.status === 'active' ? 'networks.table.statusActive' : 'networks.table.statusDraft';
        return (
          <StatusPill variant={variant} size="sm">
            {t(labelKey)}
          </StatusPill>
        );
      },
      meta: { size: 'w-[110px]' },
    },
  ];
}
