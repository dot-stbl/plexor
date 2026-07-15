import type { TFunction } from 'i18next';
import type { ColumnDef } from '@/shared/ui/data-table';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Size } from '@/shared/ui/primitives/size';
import { Badge } from '@/shared/ui/primitives/badge';
import { TechIcon } from '@/shared/ui/primitives/tech-icon';
import { CopyableText } from '@/shared/ui/primitives/copyable-text';
import type { K8sCluster } from './k8s-types';
import { mapK8sStatusToVariant } from './k8s-types';

/**
 * Columns for the managed K3s cluster list. Fleet totals (vCPU / RAM) summed
 * across nodes; `endpoint` is copyable (API server URL).
 */
export function getK8sColumns(t: TFunction): ColumnDef<K8sCluster>[] {
  return [
    {
      id: 'name',
      header: t('table.name'),
      accessorKey: 'name',
      cell: ({ row }) => (
        <span className="flex items-center gap-2">
          <TechIcon slug="kubernetes" className="size-4 shrink-0" />
          <span className="font-medium">{row.original.name}</span>
        </span>
      ),
      meta: { filter: { type: 'text', param: 'name', placeholder: t('table.filter.searchName') } },
    },
    {
      id: 'status',
      header: t('table.status'),
      accessorKey: 'status',
      cell: ({ row }) => (
        <StatusPill variant={mapK8sStatusToVariant(row.original.status)} size="sm">
          {row.original.status}
        </StatusPill>
      ),
      meta: {
        size: 'w-[120px]',
        filter: {
          type: 'select',
          param: 'status',
          placeholder: t('table.filter.status'),
          options: [
            { value: 'running', label: 'running' },
            { value: 'provisioning', label: 'provisioning' },
            { value: 'degraded', label: 'degraded' },
            { value: 'error', label: 'error' },
          ],
        },
      },
    },
    {
      id: 'version',
      header: t('table.version'),
      accessorKey: 'version',
      cell: ({ getValue }) => <MonoNum muted>{getValue<string>()}</MonoNum>,
    },
    {
      id: 'nodes',
      header: t('table.nodes'),
      accessorKey: 'cpNodes',
      cell: ({ row }) => (
        <span>
          <MonoNum>{row.original.cpNodes}</MonoNum> cp ·{' '}
          <MonoNum>{row.original.workerNodes}</MonoNum> wk
        </span>
      ),
      meta: { size: 'w-[120px]' },
    },
    {
      id: 'vcpu',
      header: t('table.vcpu'),
      accessorKey: 'vcpu',
      cell: ({ getValue }) => <MonoNum>{getValue<number>()}</MonoNum>,
      meta: { size: 'w-[80px]', align: 'right' },
    },
    {
      id: 'ram',
      header: t('table.ram'),
      accessorKey: 'ramBytes',
      cell: ({ getValue }) => <Size bytes={getValue<number>()} />,
      meta: { size: 'w-[100px]', align: 'right' },
    },
    {
      id: 'cni',
      header: t('table.cni'),
      accessorKey: 'cni',
      cell: ({ getValue }) => <Badge variant="outline">{getValue<string>()}</Badge>,
      meta: { size: 'w-[110px]' },
    },
    {
      id: 'fleet',
      header: t('table.fleet'),
      accessorKey: 'fleet',
      cell: ({ getValue }) => <MonoNum muted>{getValue<string>()}</MonoNum>,
    },
    {
      id: 'endpoint',
      header: t('table.endpoint'),
      accessorKey: 'endpoint',
      cell: ({ row }) => (
        <CopyableText value={row.original.endpoint}>{row.original.endpoint}</CopyableText>
      ),
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
