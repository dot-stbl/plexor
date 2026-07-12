import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

/**
 * SegmentedControl — 2-4 взаимоисключающих варианта (эталон YC: Авто/Из списка,
 * standard/cpu-opt/mem-opt, Вручную/Сгенерировать). Selected = **ink**
 * (bg-accent), НЕ синий. radiogroup-семантика. Для длинных списков — Select,
 * для карт-пресетов — SelectableCardGrid.
 */
export interface SegmentedOption<T extends string> {
  value: T;
  label: ReactNode;
  disabled?: boolean;
}

export interface SegmentedControlProps<T extends string> {
  value: T;
  onValueChange: (value: T) => void;
  options: SegmentedOption<T>[];
  size?: 'sm' | 'md';
  className?: string;
  'aria-label'?: string;
}

export function SegmentedControl<T extends string>({
  value,
  onValueChange,
  options,
  size = 'md',
  className,
  ...props
}: SegmentedControlProps<T>) {
  return (
    <div
      role="radiogroup"
      aria-label={props['aria-label']}
      className={cn(
        'inline-flex w-fit items-center gap-0.5 rounded-md border border-border bg-surface-2 p-0.5',
        className,
      )}
    >
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={selected}
            disabled={option.disabled}
            onClick={() => onValueChange(option.value)}
            className={cn(
              'rounded-[5px] px-2.5 font-medium whitespace-nowrap outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring/40 disabled:pointer-events-none disabled:opacity-50',
              size === 'sm' ? 'h-6 text-[11px]' : 'h-7 text-xs',
              selected
                ? 'bg-accent text-accent-foreground shadow-sm'
                : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
