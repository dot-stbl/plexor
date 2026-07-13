import { useState } from 'react';
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/ui/primitives/popover';
import { Button } from '@/shared/ui/primitives/button';
import { Checkbox } from '@/shared/ui/primitives/checkbox';
import { SlidersHorizontal, DotsSixVertical } from '@/shared/ui/icon';
import { cn } from '@/lib/utils';
import type { ColumnDef } from './data-table';

/** Состояние менеджера колонок — сериализуемо (для `useLocalStorage`). */
export interface DataTableColumnsState {
  hidden: string[];
  order: string[];
}

export interface DataTableColumnsProps<TData> {
  columns: ColumnDef<TData>[];
  value: DataTableColumnsState;
  onChange: (next: DataTableColumnsState) => void;
}

/**
 * Менеджер колонок таблицы (эталон YC): шестерёнка → popover со списком колонок
 * (чекбокс видимости + drag-хэндл порядка). Первая колонка запинена (всегда
 * видима). Применяется live; состояние — сериализуемое (пейджа хранит в
 * localStorage). Скрытие/порядок применяет `DataTable` через `hiddenColumns`/
 * `columnOrder`.
 */
export function DataTableColumns<TData>({ columns, value, onChange }: DataTableColumnsProps<TData>) {
  const [dragId, setDragId] = useState<string | null>(null);

  const ids = columns.map((c) => c.id as string).filter(Boolean);
  // Эффективный порядок: валидные из value.order + новые колонки в хвост.
  const order = [
    ...value.order.filter((id) => ids.includes(id)),
    ...ids.filter((id) => !value.order.includes(id)),
  ];
  const hidden = new Set(value.hidden);
  const labelOf = (id: string) => {
    const header = columns.find((c) => c.id === id)?.header;
    return (typeof header === 'string' && header) || id;
  };

  const toggle = (id: string) => {
    const next = new Set(hidden);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    onChange({ hidden: [...next], order });
  };

  const reorder = (from: string, to: string) => {
    if (from === to) return;
    const arr = [...order];
    const fromIdx = arr.indexOf(from);
    const toIdx = arr.indexOf(to);
    if (fromIdx < 0 || toIdx < 0) return;
    arr.splice(fromIdx, 1);
    arr.splice(toIdx, 0, from);
    onChange({ hidden: [...hidden], order: arr });
  };

  return (
    <Popover>
      <PopoverTrigger
        render={
          <Button variant="outline" size="icon-sm" aria-label="Configure columns">
            <SlidersHorizontal className="size-3.5" />
          </Button>
        }
      />
      <PopoverContent align="end" className="w-56 p-1">
        <div className="px-2 py-1.5 text-[11px] font-medium tracking-[0.04em] text-muted-foreground uppercase">
          Columns
        </div>
        <div className="flex flex-col">
          {order.map((id, idx) => {
            const locked = idx === 0; // первая колонка всегда видима
            const visible = !hidden.has(id);
            return (
              <div
                key={id}
                draggable={!locked}
                onDragStart={() => setDragId(id)}
                onDragOver={(e) => e.preventDefault()}
                onDrop={() => {
                  if (dragId) reorder(dragId, id);
                  setDragId(null);
                }}
                onDragEnd={() => setDragId(null)}
                className={cn(
                  'flex items-center gap-2 rounded-sm px-2 py-1.5 text-xs transition-colors hover:bg-muted',
                  !locked && 'cursor-grab active:cursor-grabbing',
                  dragId === id && 'opacity-50',
                )}
              >
                <DotsSixVertical className={cn('size-3.5 shrink-0 text-muted-foreground', locked && 'opacity-30')} />
                <Checkbox
                  checked={visible}
                  disabled={locked}
                  onCheckedChange={() => toggle(id)}
                  aria-label={`Column "${labelOf(id)}"`}
                />
                <span className="min-w-0 flex-1 truncate">{labelOf(id)}</span>
              </div>
            );
          })}
        </div>
      </PopoverContent>
    </Popover>
  );
}
