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
import { Input } from '@/shared/ui/primitives/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import { cn } from '@/lib/utils';

/**
 * Project-specific column declaration. Augments TanStack Table with the
 * metadata our screens need: filter UI per column, query-param key for
 * server-side filtering, alignment, fixed width.
 *
 * The DataTable primitive reads `meta` to render the filterable header and
 * the checkbox column. The screen passes the same column list to its
 * `useReactTable` config AND to `useDataTableState` (the screen owns the
 * filter → query-param mapping).
 */
export interface ColumnMeta {
  /** Visual width class (e.g. `w-[110px]`). Omit for flex columns. */
  size?: string;
  align?: 'left' | 'right';
  /** Filter UI rendered in the header row. */
  filter?: ColumnFilter;
}

export type ColumnFilter =
  | { type: 'text'; param: string; placeholder?: string }
  | { type: 'select'; param: string; options: { value: string; label: string }[]; placeholder?: string }
  | { type: 'none' };

/**
 * Project-specific column declaration built on TanStack's `ColumnDef`. The
 * `meta` field carries our screen-level concerns (filter UI, alignment,
 * fixed width) and is read by the DataTable primitive. Keeping it
 * structurally compatible with TanStack's discriminated union (rather
 * than `Omit<…>`-ing it) lets `accessorKey` / `accessorFn` work normally.
 */
export type ColumnDef<TData> = TanstackColumnDef<TData, unknown> & {
  meta?: ColumnMeta;
};

/**
 * Filter values keyed by `filter.param` (the API query-param name).
 * E.g. `{ status: 'running', q: 'web', zone: 'eu-central-1' }`.
 */
export type FilterValues = Record<string, string>;

/**
 * Generic table that:
 *   - takes a typed column list with filter declarations
 *   - renders shadcn Table rows from TanStack state
 *   - renders a second header row with filter UIs (text input / select)
 *   - exposes checkbox col + select-all via the `selection` prop
 *
 * The screen owns the filter → API mapping. DataTable is purely presentational.
 */
export interface DataTableProps<TData> {
  columns: ColumnDef<TData>[];
  data: TData[];
  /** Initial row count from server; used for skeleton/etc. */
  totalCount: number;
  /** Filter state, lifted to the parent. */
  filters: FilterValues;
  onFiltersChange: (next: FilterValues) => void;
  /** Selection — when provided, renders a checkbox column. */
  selection?: {
    selectedIds: ReadonlySet<string>;
    onToggle: (id: string) => void;
    onToggleAll: (next: boolean) => void;
  };
  onRowClick?: (row: TData) => void;
  /** Optional row-id accessor (default: `row.id`). */
  getRowId?: (row: TData) => string;
  /** Optional className for the table wrapper. */
  className?: string;
}

export function DataTable<TData>({
  columns,
  data,
  filters,
  onFiltersChange,
  selection,
  onRowClick,
  getRowId,
  className,
}: DataTableProps<TData>) {
  const idAccessor = getRowId ?? ((row: any) => row.id as string);
  const selectionEnabled = !!selection;

  // Build the TanStack column list with selection column merged in.
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
          {/* Filter row — one input/select per column that declares filter.meta */}
          <TableRow className="border-b-0 hover:bg-transparent">
            {tableColumns.map((column) => {
              const meta = (column.meta ?? {}) as ColumnMeta;
              const filter = meta.filter;
              if (!filter || filter.type === 'none') {
                return <TableHead key={column.id as string} className={cn('p-2', meta.size)} />;
              }
              return (
                <TableHead key={column.id as string} className={cn('p-2', meta.size)}>
                  <ColumnFilterInput
                    filter={filter}
                    value={filters[filter.param] ?? ''}
                    onChange={(next) => {
                      const updated = { ...filters };
                      if (next === '' || next == null) delete updated[filter.param];
                      else updated[filter.param] = next;
                      onFiltersChange(updated);
                    }}
                  />
                </TableHead>
              );
            })}
          </TableRow>
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

interface ColumnFilterInputProps {
  filter: Exclude<ColumnFilter, { type: 'none' }>;
  value: string;
  onChange: (next: string) => void;
}

function ColumnFilterInput({ filter, value, onChange }: ColumnFilterInputProps) {
  if (filter.type === 'select') {
    return (
      <Select
        items={[{ value: '', label: filter.placeholder ?? 'Все' }, ...filter.options]}
        value={value}
        onValueChange={(v) => onChange(v ?? '')}
      >
        <SelectTrigger size="sm" className="w-full text-xs">
          <SelectValue placeholder={filter.placeholder ?? 'Все'} />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="">{filter.placeholder ?? 'Все'}</SelectItem>
          {filter.options.map((opt) => (
            <SelectItem key={opt.value} value={opt.value}>
              {opt.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    );
  }
  return (
    <Input
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={filter.placeholder}
      className="h-7 text-xs"
    />
  );
}

/**
 * Build initial filter values from the column declarations — useful when
 * resetting filters in the parent.
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
 * Strip out any empty-string filter values; pass-through for everything
 * else. Lets the parent pass `FilterValues` straight to the API client.
 */
export function compactFilters(filters: FilterValues): FilterValues {
  const result: FilterValues = {};
  for (const [k, v] of Object.entries(filters)) {
    if (v !== '' && v != null) result[k] = v;
  }
  return result;
}