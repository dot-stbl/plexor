import type { TFunction } from 'i18next';
import type { ColumnDef } from '@/shared/ui/data-table';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Size } from '@/shared/ui/primitives/size';
import { Badge } from '@/shared/ui/primitives/badge';
import { TechIcon } from '@/shared/ui/primitives/tech-icon';
import { Image } from '@nine-thirty-five/material-symbols-react/rounded/700';
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
export function getImageColumns(t: TFunction): ColumnDef<OsImage>[] {
  return [
    {
      id: 'name',
      header: t('table.name'),
      accessorKey: 'name',
      cell: ({ row }) => (
        <span className="flex items-center gap-2">
          <TechIcon slug={row.original.techSlug ?? ''} fallback={Image} className="size-4 shrink-0" />
          <span className="truncate font-medium text-foreground">{row.original.name}</span>
        </span>
      ),
      meta: { filter: { type: 'text', param: 'name', placeholder: t('table.filter.searchName') } },
    },
    {
      id: 'os',
      header: t('table.os'),
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
      header: t('table.arch'),
      accessorKey: 'arch',
      cell: ({ getValue }) => (
        <Badge variant="outline" className="font-mono text-[11px]">
          {getValue<string>()}
        </Badge>
      ),
      meta: {
        size: 'w-[90px]',
        filter: {
          type: 'select',
          param: 'arch',
          placeholder: t('table.filter.arch'),
          options: [
            { value: 'x86_64', label: 'x86_64' },
            { value: 'arm64', label: 'arm64' },
          ],
        },
      },
    },
    {
      id: 'size',
      header: t('table.size'),
      accessorKey: 'sizeBytes',
      cell: ({ getValue }) => <Size bytes={getValue<number>()} />,
      meta: { size: 'w-[100px]', align: 'right' },
    },
    {
      id: 'minDisk',
      header: t('table.minDisk'),
      accessorKey: 'minDiskBytes',
      cell: ({ getValue }) => <Size bytes={getValue<number>()} muted />,
      meta: { size: 'w-[100px]', align: 'right' },
    },
    {
      id: 'visibility',
      header: t('table.visibility'),
      accessorKey: 'visibility',
      cell: ({ getValue }) =>
        getValue<string>() === 'public' ? (
          <Badge variant="secondary">public</Badge>
        ) : (
          <Badge variant="outline">private</Badge>
        ),
      meta: {
        size: 'w-[110px]',
        filter: {
          type: 'select',
          param: 'visibility',
          placeholder: t('table.filter.visibility'),
          options: [
            { value: 'public', label: 'public' },
            { value: 'private', label: 'private' },
          ],
        },
      },
    },
    {
      id: 'status',
      header: t('table.status'),
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
      header: t('table.created'),
      accessorKey: 'createdAt',
      cell: ({ getValue }) => <MonoNum muted>{getValue<string>().slice(0, 10)}</MonoNum>,
      meta: { size: 'w-[120px]' },
    },
  ];
}
