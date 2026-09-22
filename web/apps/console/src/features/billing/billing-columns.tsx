import type { TFunction } from 'i18next';
import type { ColumnDef } from '@/shared/ui/data-table';
import type { BillingInvoice } from '@/shared/api/mocks/handmade/billing';
import { formatAmount } from '@/shared/api/mocks/handmade/billing';
import { StatusPill } from '@/shared/ui/primitives/status-pill';

/** Колонки таблицы счетов. Цена в minor-units → decimal строка; статус — pill. */
export function getInvoiceColumns(t: TFunction): ColumnDef<BillingInvoice>[] {
  return [
    {
      id: 'number',
      header: t('billing.invoices.number'),
      accessorKey: 'number',
      cell: ({ row }) => <span className="font-mono text-xs">{row.original.number}</span>,
    },
    {
      id: 'issuedAt',
      header: t('billing.invoices.issued'),
      accessorKey: 'issuedAt',
      cell: ({ getValue }) => (
        <span className="font-mono text-xs tabular-nums text-muted-foreground">
          {getValue<string>().slice(0, 10)}
        </span>
      ),
      meta: { size: 'w-[120px]' },
    },
    {
      id: 'amount',
      header: t('billing.invoices.amount'),
      accessorKey: 'amountMinor',
      cell: ({ row }) => (
        <span className="font-mono text-xs tabular-nums">
          {formatAmount(row.original.amountMinor, row.original.currency)}
        </span>
      ),
      meta: { size: 'w-[140px]', align: 'right' },
    },
    {
      id: 'status',
      header: t('billing.invoices.status'),
      accessorKey: 'status',
      cell: ({ row }) => {
        const variant =
          row.original.status === 'paid' ? 'ok' : row.original.status === 'open' ? 'pending' : 'stopped';
        const labelKey = `billing.invoices.status${row.original.status[0]!.toUpperCase()}${row.original.status.slice(1)}` as const;
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
