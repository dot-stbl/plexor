import type { ColumnDef } from '@/shared/ui/data-table';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Size } from '@/shared/ui/primitives/size';
import { Badge } from '@/shared/ui/primitives/badge';
import { TechIcon } from '@/shared/ui/primitives/tech-icon';
import { Image } from '@/shared/ui/icon';
import type { ImageStatus, OsImage } from './image-types';
import { mapImageStatusToVariant } from './image-types';

const STATUS_LABEL: Record<ImageStatus, string> = {
  ready: 'ready',
  creating: 'building',
  error: 'error',
};

/**
 * Колонки каталога образов (эталон YC «Образы»): бренд-логотип дистрибутива,
 * ОС+версия, архитектура, размеры через `Size` (self-hosted — точные байты).
 */
export const imageColumns: ColumnDef<OsImage>[] = [
  {
    id: 'name',
    header: 'Name',
    accessorKey: 'name',
    cell: ({ row }) => (
      <span className="flex items-center gap-2">
        <TechIcon slug={row.original.techSlug ?? ''} fallback={Image} className="size-4 shrink-0" />
        <span className="truncate font-medium text-foreground">{row.original.name}</span>
      </span>
    ),
  },
  {
    id: 'os',
    header: 'OS',
    accessorKey: 'os',
    cell: ({ row }) => (
      <span className="flex items-baseline gap-1.5">
        <span className="text-foreground">{row.original.os}</span>
        <MonoNum muted>{row.original.version}</MonoNum>
      </span>
    ),
  },
  {
    id: 'arch',
    header: 'Arch',
    accessorKey: 'arch',
    cell: ({ getValue }) => (
      <Badge variant="outline" className="font-mono text-[11px]">
        {getValue<string>()}
      </Badge>
    ),
    meta: { size: 'w-[90px]' },
  },
  {
    id: 'size',
    header: 'Size',
    accessorKey: 'sizeBytes',
    cell: ({ getValue }) => <Size bytes={getValue<number>()} />,
    meta: { size: 'w-[100px]', align: 'right' },
  },
  {
    id: 'minDisk',
    header: 'Min disk',
    accessorKey: 'minDiskBytes',
    cell: ({ getValue }) => <Size bytes={getValue<number>()} muted />,
    meta: { size: 'w-[100px]', align: 'right' },
  },
  {
    id: 'visibility',
    header: 'Visibility',
    accessorKey: 'visibility',
    cell: ({ getValue }) =>
      getValue<string>() === 'public' ? (
        <Badge variant="secondary">public</Badge>
      ) : (
        <Badge variant="outline">private</Badge>
      ),
    meta: { size: 'w-[110px]' },
  },
  {
    id: 'status',
    header: 'Status',
    accessorKey: 'status',
    cell: ({ row }) => (
      <StatusPill variant={mapImageStatusToVariant(row.original.status)} size="sm">
        {STATUS_LABEL[row.original.status]}
      </StatusPill>
    ),
    meta: { size: 'w-[100px]' },
  },
  {
    id: 'created',
    header: 'Created',
    accessorKey: 'createdAt',
    cell: ({ getValue }) => <MonoNum muted>{getValue<string>().slice(0, 10)}</MonoNum>,
    meta: { size: 'w-[120px]' },
  },
];
