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
export const k8sColumns: ColumnDef<K8sCluster>[] = [
  {
    id: 'name',
    header: 'Name',
    accessorKey: 'name',
    cell: ({ row }) => (
      <span className="flex items-center gap-2">
        <TechIcon slug="kubernetes" className="size-4 shrink-0" />
        <span className="font-medium">{row.original.name}</span>
      </span>
    ),
  },
  {
    id: 'status',
    header: 'Status',
    accessorKey: 'status',
    cell: ({ row }) => (
      <StatusPill variant={mapK8sStatusToVariant(row.original.status)} size="sm">
        {row.original.status}
      </StatusPill>
    ),
    meta: { size: 'w-[120px]' },
  },
  {
    id: 'version',
    header: 'Version',
    accessorKey: 'version',
    cell: ({ getValue }) => <MonoNum muted>{getValue<string>()}</MonoNum>,
  },
  {
    id: 'nodes',
    header: 'Nodes',
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
    header: 'vCPU',
    accessorKey: 'vcpu',
    cell: ({ getValue }) => <MonoNum>{getValue<number>()}</MonoNum>,
    meta: { size: 'w-[80px]', align: 'right' },
  },
  {
    id: 'ram',
    header: 'RAM',
    accessorKey: 'ramBytes',
    cell: ({ getValue }) => <Size bytes={getValue<number>()} />,
    meta: { size: 'w-[100px]', align: 'right' },
  },
  {
    id: 'cni',
    header: 'CNI',
    accessorKey: 'cni',
    cell: ({ getValue }) => <Badge variant="outline">{getValue<string>()}</Badge>,
    meta: { size: 'w-[110px]' },
  },
  {
    id: 'fleet',
    header: 'Fleet',
    accessorKey: 'fleet',
    cell: ({ getValue }) => <MonoNum muted>{getValue<string>()}</MonoNum>,
  },
  {
    id: 'endpoint',
    header: 'Endpoint',
    accessorKey: 'endpoint',
    cell: ({ row }) => (
      <CopyableText value={row.original.endpoint}>{row.original.endpoint}</CopyableText>
    ),
  },
  {
    id: 'created',
    header: 'Created',
    accessorKey: 'createdAt',
    cell: ({ getValue }) => <MonoNum muted>{getValue<string>().slice(0, 10)}</MonoNum>,
    meta: { size: 'w-[120px]' },
  },
];
