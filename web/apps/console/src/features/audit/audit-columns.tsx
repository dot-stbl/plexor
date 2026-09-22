import type { TFunction } from 'i18next';
import type { ColumnDef } from '@/shared/ui/data-table';
import type { AuditEvent } from './audit-types';
import { formatAuditTimestamp } from './audit-types';

/**
 * Колонки таблицы событий аудита. Колонки «Действие / Актор / Цель /
 * Когда» — всё, что нужно для быстрого скана timeline'а; payload прячем,
 * чтобы строка оставалась читаемой. Wire-action выводим моноширинно —
 * это dot.case id, и в нём важны точка/регистр.
 */
export function getAuditColumns(t: TFunction): ColumnDef<AuditEvent>[] {
  return [
    {
      id: 'action',
      header: t('table.action'),
      accessorKey: 'action',
      cell: ({ row }) => (
        <span className="font-mono text-xs text-foreground">{row.original.action}</span>
      ),
    },
    {
      id: 'actor',
      header: t('table.actor'),
      accessorKey: 'actorUserId',
      cell: ({ row }) =>
        row.original.actorUserId ? (
          <span className="font-mono text-xs text-muted-foreground">{row.original.actorUserId}</span>
        ) : (
          <span className="text-xs text-muted-foreground">—</span>
        ),
      meta: { size: 'w-[220px]' },
    },
    {
      id: 'target',
      header: t('table.target'),
      accessorKey: 'targetKind',
      cell: ({ row }) =>
        row.original.targetId ? (
          <span className="font-mono text-xs text-muted-foreground">
            {row.original.targetKind}:{row.original.targetId}
          </span>
        ) : (
          <span className="font-mono text-xs text-muted-foreground">{row.original.targetKind}</span>
        ),
    },
    {
      id: 'occurredAt',
      header: t('table.occurredAt'),
      accessorKey: 'occurredAt',
      cell: ({ getValue }) => (
        <span className="font-mono text-xs tabular-nums text-muted-foreground">
          {formatAuditTimestamp(getValue<string>())}
        </span>
      ),
      meta: { size: 'w-[180px]', align: 'right' },
    },
  ];
}
