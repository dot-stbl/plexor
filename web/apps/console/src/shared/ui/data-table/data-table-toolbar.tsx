import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { FilterAltOff } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Input } from '@/shared/ui/primitives/input';
import { Button } from '@/shared/ui/primitives/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import { cn } from '@/lib/utils';
import type { ColumnDef, FilterValues } from './data-table';
import { DataTableColumns, type DataTableColumnsState } from './data-table-columns';

export interface DataTableToolbarProps<TData = unknown> {
  columns: ColumnDef<TData>[];
  filters: FilterValues;
  onFiltersChange: (next: FilterValues) => void;
  /**
   * When provided, the column-manager gear renders on the right of the bar —
   * so filters + column settings live in ONE toolbar. Omit to hide the gear.
   */
  columnsState?: DataTableColumnsState;
  onColumnsChange?: (next: DataTableColumnsState) => void;
  /** Optional leading slot (e.g. a selection badge). */
  leading?: ReactNode;
  /** Optional trailing slot (rendered after the gear). */
  trailing?: ReactNode;
  className?: string;
}

/**
 * Filter + column-settings bar rendered ABOVE the table. Filters are DERIVED
 * from the column declarations (`meta.filter` of type `text` | `select`), so a
 * list gets filtering by declaring it once on its columns. Server-backed lists
 * feed `onFiltersChange` → `compactFilters` → API; local lists feed
 * `applyFilters(rows, filters, columns)`. Pass `columnsState`/`onColumnsChange`
 * to fold the column-manager gear into the same bar.
 */
export function DataTableToolbar<TData = unknown>({
  columns,
  filters,
  onFiltersChange,
  columnsState,
  onColumnsChange,
  leading,
  trailing,
  className,
}: DataTableToolbarProps<TData>) {
  const { t } = useTranslation();

  const filterCols: Array<{
    id: string;
    param: string;
    filter: Exclude<NonNullable<ColumnDef<TData>['meta']>['filter'], { type: 'none' } | undefined>;
  }> = [];
  for (const col of columns) {
    const f = col.meta?.filter;
    if (f && f.type !== 'none') {
      filterCols.push({ id: col.id as string, param: f.param, filter: f });
    }
  }

  const update = (param: string, next: string) => {
    const updated = { ...filters };
    if (next === '' || next == null) delete updated[param];
    else updated[param] = next;
    onFiltersChange(updated);
  };

  const hasFilters = Object.values(filters).some((v) => v !== '' && v != null);
  const showColumns = !!columnsState && !!onColumnsChange;

  const reset = () => {
    const cleared: FilterValues = {};
    for (const { param } of filterCols) cleared[param] = '';
    onFiltersChange(cleared);
  };

  // No card wrapper — a bare bar. Filters (+ reset) on the LEFT, column
  // settings pinned to the RIGHT (space-between).
  return (
    <div data-od-id="data-table-toolbar" className={cn('flex items-center justify-between gap-2', className)}>
      <div className="flex min-w-0 flex-wrap items-center gap-2">
        {leading}
        {filterCols.map(({ id, param, filter }) => (
          <FilterControl
            key={id}
            filter={filter}
            value={filters[param] ?? ''}
            onChange={(next) => update(param, next)}
          />
        ))}
        {hasFilters && (
          <Button variant="ghost" size="icon-sm" aria-label={t('common.resetFilters')} onClick={reset}>
            <FilterAltOff className="size-3.5" />
          </Button>
        )}
      </div>

      <div className="flex shrink-0 items-center gap-2">
        {trailing}
        {showColumns && (
          <DataTableColumns columns={columns} value={columnsState} onChange={onColumnsChange} />
        )}
      </div>
    </div>
  );
}

type ActiveFilter = Exclude<
  NonNullable<ColumnDef<unknown>['meta']>['filter'],
  { type: 'none' } | undefined
>;

interface FilterControlProps {
  filter: ActiveFilter;
  value: string;
  onChange: (next: string) => void;
}

function FilterControl({ filter, value, onChange }: FilterControlProps) {
  const { t } = useTranslation();
  if (filter.type === 'select') {
    const allLabel = filter.placeholder ?? t('common.all');
    return (
      <div className="min-w-[140px]">
        <Select
          items={[{ value: '', label: allLabel }, ...filter.options]}
          value={value}
          onValueChange={(v) => onChange(v ?? '')}
        >
          <SelectTrigger size="sm" className="w-full text-xs">
            <SelectValue placeholder={allLabel} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">{allLabel}</SelectItem>
            {filter.options.map((opt) => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    );
  }
  return (
    <div className="min-w-[220px] flex-1">
      <Input
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={filter.placeholder}
        className="h-7 text-xs"
        aria-label={filter.placeholder}
      />
    </div>
  );
}
