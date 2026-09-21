import type { TFunction } from 'i18next';
import type {
  VSphereInventoryClusterRow,
  VSphereInventoryHostRow,
  VSphereInventoryVirtualMachineRow,
} from '@/shared/api';
import type { ColumnDef } from '@/shared/ui/data-table';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { CopyableText } from '@/shared/ui/primitives/copyable-text';
import { mapVSpherePowerStateToVariant } from './power-state';

function buildPowerStateFilterOptions(t: TFunction): { value: 'POWERED_ON' | 'POWERED_OFF' | 'SUSPENDED'; label: string }[] {
  return [
    { value: 'POWERED_ON', label: t('vsphere.inventory.powerOn') },
    { value: 'POWERED_OFF', label: t('vsphere.inventory.powerOff') },
    { value: 'SUSPENDED', label: t('vsphere.inventory.suspended') },
  ];
}

/**
 * Column set for the vSphere cluster table. One declaration per
 * table — the screen hands the columns straight to `DataTable`.
 */
export function getVSphereClusterColumns(t: TFunction): ColumnDef<VSphereInventoryClusterRow>[] {
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
      id: 'moref',
      header: 'mo-ref',
      accessorKey: 'moref',
      cell: ({ row }) => (
        <CopyableText value={row.original.moref} copyLabel={t('table.copy.id')}>
          {row.original.moref}
        </CopyableText>
      ),
      meta: { size: 'w-[120px]' },
    },
    {
      id: 'datacenterMoref',
      header: t('vsphere.inventory.datacenter'),
      accessorKey: 'datacenterMoref',
      cell: ({ row }) => (
        <span className="font-mono text-xs text-muted-foreground">{row.original.datacenterMoref}</span>
      ),
      meta: { size: 'w-[140px]' },
    },
    {
      id: 'drsEnabled',
      header: t('vsphere.inventory.drs'),
      accessorKey: 'drsEnabled',
      cell: ({ row }) => (
        <StatusPill
          variant={row.original.drsEnabled ? 'ok' : 'idle'}
          size="sm"
        >
          {row.original.drsEnabled ? t('vsphere.inventory.drsOn') : t('vsphere.inventory.drsOff')}
        </StatusPill>
      ),
      meta: { size: 'w-[90px]', align: 'left' },
    },
  ];
}

/**
 * Column set for the vSphere ESXi host table.
 */
export function getVSphereHostColumns(t: TFunction): ColumnDef<VSphereInventoryHostRow>[] {
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
      id: 'moref',
      header: 'mo-ref',
      accessorKey: 'moref',
      cell: ({ row }) => (
        <CopyableText value={row.original.moref} copyLabel={t('table.copy.id')}>
          {row.original.moref}
        </CopyableText>
      ),
      meta: { size: 'w-[100px]' },
    },
    {
      id: 'clusterMoref',
      header: t('vsphere.inventory.cluster'),
      accessorKey: 'clusterMoref',
      cell: ({ row }) => (
        <span className="font-mono text-xs text-muted-foreground">{row.original.clusterMoref}</span>
      ),
      meta: { size: 'w-[120px]' },
    },
    {
      id: 'connectionState',
      header: t('table.status'),
      accessorKey: 'connectionState',
      cell: ({ row }) => (
        <StatusPill
          variant={
            row.original.connectionState === 'CONNECTED'
              ? 'ok'
              : row.original.connectionState === 'DISCONNECTED'
                ? 'err'
                : 'warn'
          }
          size="sm"
        >
          {row.original.connectionState}
        </StatusPill>
      ),
      meta: { size: 'w-[130px]' },
    },
    {
      id: 'cpuCores',
      header: t('table.cores'),
      accessorKey: 'cpuCores',
      cell: ({ getValue }) => <MonoNum>{getValue<number>()}</MonoNum>,
      meta: { size: 'w-[70px]', align: 'right' },
    },
    {
      id: 'memoryMib',
      header: t('table.ram'),
      accessorKey: 'memoryMib',
      cell: ({ getValue }) => (
        <>
          <MonoNum muted>{getValue<number>()}</MonoNum>
          <span className="ml-0.5 text-muted-foreground">MiB</span>
        </>
      ),
      meta: { size: 'w-[110px]', align: 'right' },
    },
  ];
}

/**
 * Column set for the vSphere VM table.
 */
export function getVSphereVmColumns(t: TFunction): ColumnDef<VSphereInventoryVirtualMachineRow>[] {
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
      id: 'moref',
      header: 'mo-ref',
      accessorKey: 'moref',
      cell: ({ row }) => (
        <CopyableText value={row.original.moref} copyLabel={t('table.copy.id')}>
          {row.original.moref}
        </CopyableText>
      ),
      meta: { size: 'w-[100px]' },
    },
    {
      id: 'powerState',
      header: t('table.status'),
      accessorKey: 'powerState',
      cell: ({ row }) => (
        <StatusPill variant={mapVSpherePowerStateToVariant(row.original.powerState)} size="sm">
          {row.original.powerState}
        </StatusPill>
      ),
      meta: {
        size: 'w-[120px]',
        filter: {
          type: 'select',
          param: 'powerState',
          options: buildPowerStateFilterOptions(t),
          placeholder: t('vsphere.inventory.powerState'),
        },
      },
    },
    {
      id: 'folderPath',
      header: t('vsphere.inventory.folder'),
      accessorKey: 'folderPath',
      cell: ({ row }) =>
        row.original.folderPath ? (
          <span className="font-mono text-xs text-muted-foreground">{row.original.folderPath}</span>
        ) : (
          <span className="text-xs text-muted-foreground/60">—</span>
        ),
      meta: { size: 'w-[260px]' },
    },
    {
      id: 'hostMoref',
      header: t('vsphere.inventory.host'),
      accessorKey: 'hostMoref',
      cell: ({ row }) =>
        row.original.hostMoref ? (
          <span className="font-mono text-xs text-muted-foreground">{row.original.hostMoref}</span>
        ) : (
          <span className="text-xs text-muted-foreground/60">—</span>
        ),
      meta: { size: 'w-[110px]' },
    },
    {
      id: 'cpuCount',
      header: t('table.vcpu'),
      accessorKey: 'cpuCount',
      cell: ({ getValue }) => <MonoNum>{getValue<number>()}</MonoNum>,
      meta: { size: 'w-[70px]', align: 'right' },
    },
    {
      id: 'memoryMib',
      header: t('table.ram'),
      accessorKey: 'memoryMib',
      cell: ({ getValue }) => (
        <>
          <MonoNum muted>{getValue<number>()}</MonoNum>
          <span className="ml-0.5 text-muted-foreground">MiB</span>
        </>
      ),
      meta: { size: 'w-[110px]', align: 'right' },
    },
  ];
}
