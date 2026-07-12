import type { ReactNode } from 'react';
import { RadioGroup, RadioGroupItem } from '@/shared/ui/primitives/radio-group';
import { cn } from '@/lib/utils';

/**
 * SelectableCardGrid — сетка выбираемых карт (эталон YC: пресеты «2 vCPU · 8 ГБ»,
 * большие option-карты «Высокодоступный/Базовый» + badge «Рекомендуемый»).
 * Selected = **ink** border + tint, НЕ синий. RadioGroup-семантика.
 * (RuntimePicker — специализированный вариант этого паттерна.)
 */
export interface SelectableCardOption<T extends string> {
  value: T;
  title: ReactNode;
  subtitle?: ReactNode;
  /** Мелкий тег справа от title (напр. «Рекомендуемый»). */
  badge?: ReactNode;
  disabled?: boolean;
  /** Причина недоступности (под subtitle). */
  reason?: ReactNode;
}

export interface SelectableCardGridProps<T extends string> {
  value: T | undefined;
  onValueChange: (value: T) => void;
  options: SelectableCardOption<T>[];
  columns?: 1 | 2 | 3;
  className?: string;
}

const COLS: Record<1 | 2 | 3, string> = {
  1: 'grid-cols-1',
  2: 'grid-cols-1 sm:grid-cols-2',
  3: 'grid-cols-1 sm:grid-cols-3',
};

export function SelectableCardGrid<T extends string>({
  value,
  onValueChange,
  options,
  columns = 2,
  className,
}: SelectableCardGridProps<T>) {
  return (
    <RadioGroup
      value={value ?? ''}
      onValueChange={(v) => onValueChange(v as T)}
      className={cn('grid gap-2', COLS[columns], className)}
    >
      {options.map((option) => {
        const selected = value === option.value;
        return (
          <label
            key={option.value}
            data-disabled={option.disabled}
            data-selected={selected}
            className={cn(
              'flex items-start gap-2 rounded-lg border p-3 transition-colors',
              option.disabled ? 'pointer-events-none opacity-55' : 'cursor-pointer',
              selected ? 'border-primary bg-primary/5' : 'border-border hover:border-border-2',
            )}
          >
            <RadioGroupItem value={option.value} disabled={option.disabled} className="mt-0.5" />
            <span className="flex min-w-0 flex-1 flex-col gap-0.5">
              <span className="flex items-center gap-1.5 text-sm font-medium text-foreground">
                {option.title}
                {option.badge && (
                  <span className="rounded bg-surface-3 px-1 text-[10px] font-medium tracking-[0.04em] text-muted-foreground uppercase">
                    {option.badge}
                  </span>
                )}
              </span>
              {option.subtitle && <span className="text-xs text-muted-foreground">{option.subtitle}</span>}
              {option.disabled && option.reason && (
                <span className="text-[11px] text-muted-foreground">{option.reason}</span>
              )}
            </span>
          </label>
        );
      })}
    </RadioGroup>
  );
}
