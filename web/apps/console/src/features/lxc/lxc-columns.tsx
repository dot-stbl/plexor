import type { TFunction } from 'i18next';
import type { ColumnDef } from '@/shared/ui/data-table';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Size } from '@/shared/ui/primitives/size';
import { Badge } from '@/shared/ui/primitives/badge';
import { CopyableText } from '@/shared/ui/primitives/copyable-text';
import { TechIcon } from '@/shared/ui/primitives/tech-icon';
import { DeployedCode } from '@nine-thirty-five/material-symbols-react/rounded/700';
import type { LxcContainer } from './lxc-types';
import { mapLxcStatusToVariant } from './lxc-types';

/**
 * Columns for the LXC container list. Brand logo derived from the template
 * family (`ubuntu-24.04` → `ubuntu`), unprivileged/privileged as a badge, and
 * exact binary sizes via `Size` (self-hosted — real bytes, not round GBs).
 */
export function getLxcColumns(t: TFunction): ColumnDef<LxcContainer>[] {
  return [
    {
      id: 'name',
      header: t('table.name'),
      accessorKey: 'name',
      cell: ({ row }) => {
        const family = row.original.template.split('-')[0];
        return (
          <span className="flex items-center gap-2">
            <TechIcon slug={family} fallback={DeployedCode} className="size-4 shrink-0" />
            <span className="truncate font-medium text-foreground">{row.original.name}</span>
          </span>
        );
      },
      meta: { filter: { type: 'text', param: 'name', placeholder: t('table.filter.searchName') } },
    },
    {
      id: 'status',
      header: t('table.status'),
      accessorKey: 'status',
      cell: ({ row }) => (
        <StatusPill variant={mapLxcStatusToVariant(row.original.status)} size="sm">
          {row.original.status}
        </StatusPill>
      ),
      meta: {
        size: 'w-[110px]',
        filter: {
          type: 'select',
          param: 'status',
          placeholder: t('table.filter.status'),
          options: [
            { value: 'running', label: 'running' },
            { value: 'stopped', label: 'stopped' },
            { value: 'paused', label: 'paused' },
            { value: 'error', label: 'error' },
          ],
        },
      },
    },
    {
      id: 'template',
      header: t('table.template'),
      accessorKey: 'template',
      cell: ({ row }) => (
        <span className="flex items-baseline gap-1.5">
          <span className="text-foreground">{row.original.os}</span>
          <MonoNum muted>{row.original.osVersion}</MonoNum>
        </span>
      ),
    },
    {
      id: 'type',
      header: t('table.type'),
      accessorKey: 'unprivileged',
      cell: ({ row }) => (
        <Badge variant={row.original.unprivileged ? 'secondary' : 'outline'}>
          {row.original.unprivileged ? 'unprivileged' : 'privileged'}
        </Badge>
      ),
      meta: { size: 'w-[120px]' },
    },
    {
      id: 'cores',
      header: t('table.cores'),
      accessorKey: 'cores',
      cell: ({ getValue }) => <MonoNum>{getValue<number>()}</MonoNum>,
      meta: { size: 'w-[70px]', align: 'right' },
    },
    {
      id: 'memory',
      header: t('table.memory'),
      accessorKey: 'ramBytes',
      cell: ({ getValue }) => <Size bytes={getValue<number>()} />,
      meta: { size: 'w-[100px]', align: 'right' },
    },
    {
      id: 'rootfs',
      header: t('table.rootfs'),
      accessorKey: 'rootfsBytes',
      cell: ({ getValue }) => <Size bytes={getValue<number>()} muted />,
      meta: { size: 'w-[100px]', align: 'right' },
    },
    {
      id: 'node',
      header: t('table.node'),
      accessorKey: 'nodeHostname',
      cell: ({ row }) => (
        <CopyableText value={row.original.nodeHostname} copyLabel={t('table.copy.host')}>
          {row.original.nodeHostname}
        </CopyableText>
      ),
      meta: { size: 'w-[130px]' },
    },
    {
      id: 'ip',
      header: t('table.ip'),
      accessorKey: 'ip',
      cell: ({ row }) => (
        <CopyableText value={row.original.ip} copyLabel={t('table.copy.ip')}>
          {row.original.ip}
        </CopyableText>
      ),
      meta: { size: 'w-[140px]' },
    },
    {
      id: 'created',
      header: t('table.created'),
      accessorKey: 'createdAt',
      cell: ({ getValue }) => <MonoNum muted>{getValue<string>().slice(0, 10)}</MonoNum>,
      meta: { size: 'w-[120px]' },
    },
  ];
}
