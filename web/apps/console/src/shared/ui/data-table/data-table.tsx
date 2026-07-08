import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef as TanstackColumnDef,
  type TableOptions,
} from '@tanstack/react-table';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/shared/ui/primitives/table';
import { Checkbox } from '@/shared/ui/primitives/checkbox';
import { cn } from '@/lib/utils';

/**
 * Project-specific column declaration built on TanStack's `ColumnDef`. The
 * `meta` field carries our screen-level concerns (filter UI, alignment,
 * fixed width). Filter UI itself is rendered by `DataTableToolbar`, NOT
 * inside the table — the table stays a pure layout primitive.
 */
export interface ColumnMeta {
  /** Visual width class (e.g. `w-[110px]`). Omit for flex columns. */
  size?: string;
  align?: 'left' | 'right';
  /** Filter declaration consumed by `DataTableToolbar`. */
  filter?: ColumnFilter;
}

export type ColumnFilter =
  | { type: 'text'; param: string; placeholder?: string }
  | { type: 'select'; param: string; options: { value: string; label: string }[]; placeholder?: string }
  | { type: 'none' };

export type ColumnDef<TData> = TanstackColumnDef<TData, unknown> & {
  meta?: ColumnMeta;
};

/**
 * Filter values keyed by `filter.param` (the API query-param name).
 * E.g. `{ status: 'running', q: 'web', zone: 'eu-central-1' }`.
 */
export type FilterValues = Record<string, string>;

/**
 * Generic table primitive. Pure presentational — no filter row, no
 * toolbar. Filters live in `DataTableToolbar` (sibling component), which
 * the screen composes ABOVE the table in its own card. This separation
 * lets the toolbar have its own visual treatment (e.g. wrapping card)
 * without leaking filter concerns into the table.
 */
export interface DataTableProps<TData> {
  columns: ColumnDef<TData>[];
  data: TData[];
  selection?: {
    selectedIds: ReadonlySet<string>;
    onToggle: (id: string) => void;
    onToggleAll: (next: boolean) => void;
  };
  onRowClick?: (row: TData) => void;
  getRowId?: (row: TData) => string;
  className?: string;
}

export function DataTable<TData>({
  columns,
  data,
  selection,
  onRowClick,
  getRowId,
  className,
}: DataTableProps<TData>) {
  const idAccessor = getRowId ?? ((row: any) => row.id as string);
  const selectionEnabled = !!selection;

  const tableColumns: TanstackColumnDef<TData, unknown>[] = selectionEnabled
    ? ([
        {
          id: '__select',
          enableSorting: false,
          enableColumnFilter: false,
          header: () => (
            <Checkbox
              checked={
                data.length > 0 && data.every((row) => selection!.selectedIds.has(idAccessor(row)))
              }
              onCheckedChange={(value) => selection!.onToggleAll(value === true)}
              aria-label="Выбрать все"
            />
          ),
          cell: ({ row }) => (
            <Checkbox
              checked={selection!.selectedIds.has(idAccessor(row.original))}
              onCheckedChange={() => selection!.onToggle(idAccessor(row.original))}
              aria-label="Выбрать строку"
              onClick={(e) => e.stopPropagation()}
            />
          ),
          meta: { size: 'w-9' } as ColumnMeta,
        },
        ...(columns as unknown as TanstackColumnDef<TData, unknown>[]),
      ] as TanstackColumnDef<TData, unknown>[])
      : (columns as unknown as TanstackColumnDef<TData, unknown>[]);

  const table = useReactTable<TData>({
    data,
    columns: tableColumns,
    getCoreRowModel: getCoreRowModel(),
    getRowId: idAccessor,
    ...(onRowClick && {
      meta: { onRowClick },
    }),
  } as TableOptions<TData>);

  return (
    <div className={cn('rounded-lg border border-border bg-card', className)}>
      <Table>
        <TableHeader>
          {table.getHeaderGroups().map((headerGroup) => (
            <TableRow key={headerGroup.id} className="border-b-0">
              {headerGroup.headers.map((header) => {
                const meta = header.column.columnDef.meta as ColumnMeta | undefined;
                return (
                  <TableHead
                    key={header.id}
                    className={cn(meta?.size, meta?.align === 'right' && 'text-right')}
                  >
                    {header.isPlaceholder
                      ? null
                      : flexRender(header.column.columnDef.header, header.getContext())}
                  </TableHead>
                );
              })}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {table.getRowModel().rows.map((row) => (
            <TableRow
              key={row.id}
              data-state={selection?.selectedIds.has(idAccessor(row.original)) ? 'selected' : undefined}
              onClick={onRowClick ? () => onRowClick(row.original) : undefined}
              className={cn(onRowClick && 'cursor-pointer')}
            >
              {row.getVisibleCells().map((cell) => {
                const meta = cell.column.columnDef.meta as ColumnMeta | undefined;
                return (
                  <TableCell key={cell.id} className={cn(meta?.size, meta?.align === 'right' && 'text-right')}>
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </TableCell>
                );
              })}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

/**
 * Build initial filter values from the column declarations. Useful for
 * resetting filters in the parent (call `setFilters(emptyFilters(cols))`).
 */
export function emptyFilters<T>(columns: ColumnDef<T>[]): FilterValues {
  const result: FilterValues = {};
  for (const col of columns) {
    const f = (col.meta ?? {}).filter;
    if (f && f.type !== 'none') result[f.param] = '';
  }
  return result;
}

/**
 * Strip empty-string filter values; pass-through for everything else.
 * Lets the parent pass `FilterValues` straight to the API client.
 */
export function compactFilters(filters: FilterValues): FilterValues {
  const result: FilterValues = {};
  for (const [k, v] of Object.entries(filters)) {
    if (v !== '' && v != null) result[k] = v;
  }
  return result;
}