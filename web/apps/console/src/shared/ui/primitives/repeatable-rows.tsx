import type { ReactNode } from 'react';
import { Plus, X } from '@/shared/ui/icon';
import { Button } from '@/shared/ui/primitives/button';
import { cn } from '@/lib/utils';

/**
 * RepeatableRows — список строк с add/remove (эталон YC: «Добавить метку»,
 * «Добавить хост»). Generic: строку рисует `renderRow`, новую даёт `newRow`.
 */
export interface RepeatableRowsProps<T> {
  rows: T[];
  onChange: (rows: T[]) => void;
  renderRow: (row: T, update: (next: T) => void, index: number) => ReactNode;
  newRow: () => T;
  addLabel: string;
  className?: string;
}

export function RepeatableRows<T>({
  rows,
  onChange,
  renderRow,
  newRow,
  addLabel,
  className,
}: RepeatableRowsProps<T>) {
  return (
    <div className={cn('space-y-2', className)}>
      {rows.map((row, index) => (
        <div key={index} className="flex items-center gap-2">
          <div className="min-w-0 flex-1">
            {renderRow(row, (next) => onChange(rows.map((r, i) => (i === index ? next : r))), index)}
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label="Remove row"
            onClick={() => onChange(rows.filter((_, i) => i !== index))}
          >
            <X className="size-3.5" />
          </Button>
        </div>
      ))}
      <Button type="button" variant="outline" size="sm" onClick={() => onChange([...rows, newRow()])}>
        <Plus className="size-3.5" />
        {addLabel}
      </Button>
    </div>
  );
}
