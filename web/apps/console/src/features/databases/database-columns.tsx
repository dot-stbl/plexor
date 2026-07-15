import type { TFunction } from 'i18next';
import type { ColumnDef } from '@/shared/ui/data-table';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { CopyableText } from '@/shared/ui/primitives/copyable-text';
import type { DbCluster } from './database-types';
import { mapDbStatusToVariant } from './database-types';
import { RuntimeBadge } from './runtime-badge';

/**
 * Колонки таблицы кластеров ОДНОГО движка (движок задаётся разделом, поэтому
 * колонки «Движок» тут нет). Рантайм — первоклассная колонка. Бэкапы вынесены
 * отдельно — ключевое отличие managed-БД. DNS copyable.
 */
export function getDbColumns(t: TFunction): ColumnDef<DbCluster>[] {
  return [
    {
      id: 'name',
      header: t('table.name'),
      accessorKey: 'name',
      cell: ({ row }) => (
        <span className="flex items-baseline gap-1.5">
          <span className="font-medium text-foreground">{row.original.name}</span>
          <MonoNum muted>v{row.original.version}</MonoNum>
        </span>
      ),
    },
    {
      id: 'status',
      header: t('table.status'),
      accessorKey: 'status',
      cell: ({ row }) => (
        <StatusPill variant={mapDbStatusToVariant(row.original.status)} size="sm">
          {row.original.status}
        </StatusPill>
      ),
      meta: { size: 'w-[110px]' },
    },
    {
      id: 'runtime',
      header: t('table.runtime'),
      accessorKey: 'runtime',
      cell: ({ row }) => <RuntimeBadge runtime={row.original.runtime} />,
      meta: { size: 'w-[120px]' },
    },
    {
      id: 'storage',
      header: t('table.disk'),
      accessorKey: 'storageGb',
      cell: ({ getValue }) => (
        <>
          <MonoNum muted>{getValue<number>()}</MonoNum>
          <span className="ml-0.5 text-muted-foreground">GB</span>
        </>
      ),
      meta: { size: 'w-[80px]', align: 'right' },
    },
    {
      id: 'backups',
      header: t('table.backups'),
      accessorKey: 'backupsEnabled',
      cell: ({ row }) =>
        row.original.backupsEnabled ? (
          <StatusPill variant="ok" size="sm">
            on
          </StatusPill>
        ) : (
          <span className="text-xs text-muted-foreground">—</span>
        ),
      meta: { size: 'w-[90px]' },
    },
    {
      id: 'node',
      header: t('table.node'),
      accessorKey: 'hostname',
      cell: ({ row }) => (
        <CopyableText value={row.original.hostname} copyLabel={t('table.copy.host')}>
          {row.original.hostname}
        </CopyableText>
      ),
      meta: { size: 'w-[130px]' },
    },
    {
      id: 'dns',
      header: t('table.internalDns'),
      accessorKey: 'dns',
      cell: ({ row }) => (
        <CopyableText value={row.original.dns} copyLabel={t('table.copy.dns')}>
          {row.original.dns}
        </CopyableText>
      ),
    },
    {
      id: 'bindings',
      header: t('table.bindings'),
      accessorKey: 'bindings',
      cell: ({ getValue }) => <MonoNum muted>{getValue<number>()}</MonoNum>,
      meta: { size: 'w-[70px]', align: 'right' },
    },
  ];
}
