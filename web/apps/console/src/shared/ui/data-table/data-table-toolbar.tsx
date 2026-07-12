import type { ReactNode } from 'react';
import { Input } from '@/shared/ui/primitives/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import { cn } from '@/lib/utils';
import type { ColumnDef, FilterValues } from './data-table';

export interface DataTableToolbarProps<TData = unknown> {
  columns: ColumnDef<TData>[];
  filters: FilterValues;
  onFiltersChange: (next: FilterValues) => void;
  /** Optional leading slot (e.g. "X selected" badge, total count). */
  leading?: ReactNode;
  /** Optional trailing slot (e.g. "Reset filters", "Density"). */
  trailing?: ReactNode;
  /** Override the default card wrapper. */
  className?: string;
}

/**
 * Filter bar rendered ABOVE the table. Iterates `columns` and emits one
 * control per column that declares `meta.filter.type` of `text` or
 * `select`. Driven by the same column declarations as the table, so
 * adding/removing a filter is one edit in `vmColumns.tsx`.
 *
 * Compact layout — no per-control labels, placeholder + sr-only for
 * a11y. Screen-consoles pattern.
 */
export function DataTableToolbar<TData = unknown>({
  columns,
  filters,
  onFiltersChange,
  leading,
  trailing,
  className,
}: DataTableToolbarProps<TData>) {
  // Inline narrowing — keeps TS happy without a separate alias type.
  const filterCols: Array<{
    id: string;
    param: string;
    filter: Exclude<
      NonNullable<ColumnDef<TData>['meta']>['filter'],
      { type: 'none' } | undefined
    >;
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

  return (
    <div
      data-od-id="data-table-toolbar"
      className={cn(
        'flex flex-wrap items-center gap-2 rounded-lg border border-border bg-card p-2',
        className,
      )}
    >
      {leading}
      {filterCols.map(({ id, param, filter }) => {
        const value = filters[param] ?? '';
        return (
          <FilterControl
            key={id}
            filter={filter}
            value={value}
            onChange={(next) => update(param, next)}
          />
        );
      })}
      {hasFilters && (
        <button
          type="button"
          onClick={() => {
            const cleared: FilterValues = {};
            for (const { param } of filterCols) cleared[param] = '';
            onFiltersChange(cleared);
          }}
          className="ml-auto text-xs text-muted-foreground transition-colors hover:text-foreground"
        >
          Сбросить
        </button>
      )}
      {trailing}
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
  if (filter.type === 'select') {
    return (
      <div className="min-w-[140px]">
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