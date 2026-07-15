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
  /** 'compact' = tight rows (h-8 headers, h-7 cells, text-[11px]); default is comfortable. */
  density?: 'compact' | 'comfortable';
  selection?: {
    selectedIds: ReadonlySet<string>;
    onToggle: (id: string) => void;
    onToggleAll: (next: boolean) => void;
  };
  onRowClick?: (row: TData) => void;
  getRowId?: (row: TData) => string;
  /** Column ids to hide — from the column manager (`DataTableColumns`). */
  hiddenColumns?: ReadonlySet<string>;
  /** Column ids in display order — from the column manager. Missing ids keep source order at the tail. */
  columnOrder?: string[];
  className?: string;
}

/** Column id = explicit `id` or `accessorKey` fallback. */
function columnId<TData>(col: ColumnDef<TData>): string {
  return String(col.id ?? (col as { accessorKey?: string }).accessorKey ?? '');
}

/** Apply column-manager order + visibility to the declared columns. */
function orderAndFilterColumns<TData>(
  columns: ColumnDef<TData>[],
  order?: string[],
  hidden?: ReadonlySet<string>,
): ColumnDef<TData>[] {
  let result = columns;
  if (order && order.length > 0) {
    const byId = new Map(columns.map((c) => [columnId(c), c] as const));
    const ordered: ColumnDef<TData>[] = [];
    for (const id of order) {
      const c = byId.get(id);
      if (c) ordered.push(c);
    }
    for (const c of columns) {
      if (!order.includes(columnId(c))) ordered.push(c);
    }
    result = ordered;
  }
  if (hidden && hidden.size > 0) {
    result = result.filter((c) => !hidden.has(columnId(c)));
  }
  return result;
}

export function DataTable<TData>({
  columns,
  data,
  density = 'comfortable',
  selection,
  onRowClick,
  getRowId,
  hiddenColumns,
  columnOrder,
  className,
}: DataTableProps<TData>) {
  const idAccessor = getRowId ?? ((row: any) => row.id as string);
  const selectionEnabled = !!selection;
  const visibleColumns = orderAndFilterColumns(columns, columnOrder, hiddenColumns);

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
        ...(visibleColumns as unknown as TanstackColumnDef<TData, unknown>[]),
      ] as TanstackColumnDef<TData, unknown>[])
      : (visibleColumns as unknown as TanstackColumnDef<TData, unknown>[]);

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
      <Table className={cn(density === 'compact' && 'text-[11px]')}>
        <TableHeader>
          {table.getHeaderGroups().map((headerGroup) => (
            <TableRow key={headerGroup.id} className="border-b-0">
              {headerGroup.headers.map((header) => {
                const meta = header.column.columnDef.meta as ColumnMeta | undefined;
                return (
                  <TableHead
                    key={header.id}
                    className={cn(
                      density === 'compact' ? 'h-8 px-2' : 'h-10 px-2',
                      meta?.size,
                      meta?.align === 'right' && 'text-right',
                    )}
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
                  <TableCell
                    key={cell.id}
                    className={cn(
                      density === 'compact' ? 'p-2' : 'p-2',
                      meta?.size,
                      meta?.align === 'right' && 'text-right',
                    )}
                  >
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

/**
 * Client-side filtering for lists with LOCAL data (no server endpoint). Uses
 * the SAME `meta.filter` column declarations as `DataTableToolbar`, so a list
 * gets filtering by wiring one helper. Server-backed lists (e.g. VMs via MSW)
 * skip this and pass `compactFilters(filters)` to the API instead.
 *
 * Match: `text` = case-insensitive substring on the cell; `select` = exact.
 * The field read is the column's `accessorKey` (so `filter.param` should equal
 * the field name for local lists).
 */
export function applyFilters<TData>(
  rows: TData[],
  filters: FilterValues,
  columns: ColumnDef<TData>[],
): TData[] {
  const specs = columns
    .map((c) => {
      const f = c.meta?.filter;
      const key = (c as { accessorKey?: string }).accessorKey;
      if (!f || f.type === 'none' || !key) return null;
      return { param: f.param, type: f.type, key };
    })
    .filter((s): s is { param: string; type: 'text' | 'select'; key: string } => s !== null)
    .filter((s) => {
      const v = filters[s.param];
      return v != null && v !== '';
    });

  if (specs.length === 0) return rows;

  return rows.filter((row) =>
    specs.every((s) => {
      const val = filters[s.param]!;
      const cell = (row as Record<string, unknown>)[s.key];
      if (s.type === 'select') return String(cell) === val;
      return String(cell ?? '').toLowerCase().includes(val.toLowerCase());
    }),
  );
}