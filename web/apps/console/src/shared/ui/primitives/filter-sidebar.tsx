import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

/**
 * Filter-сайдбар для больших каталогов (эталон YC bare-metal): секции с чипами,
 * dual-range `Slider`, тумблерами. `FilterSidebar` — контейнер, `FilterSection`
 * — озаглавленная группа, `FilterChips` — мультивыбор-чипы (selected = ink).
 * Slider (dual-range) — существующий примитив `slider.tsx` (`value={[min,max]}`).
 */
export function FilterSidebar({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <aside data-od-id="filter-sidebar" className={cn('space-y-4', className)}>
      {children}
    </aside>
  );
}

export function FilterSection({ title, children }: { title: ReactNode; children: ReactNode }) {
  return (
    <div className="space-y-1.5">
      <div className="text-[11px] font-medium tracking-[0.04em] text-muted-foreground uppercase">{title}</div>
      {children}
    </div>
  );
}

export interface FilterChip {
  value: string;
  label: ReactNode;
}

export interface FilterChipsProps {
  options: FilterChip[];
  value: string[];
  onChange: (value: string[]) => void;
  className?: string;
}

export function FilterChips({ options, value, onChange, className }: FilterChipsProps) {
  const toggle = (v: string) =>
    onChange(value.includes(v) ? value.filter((x) => x !== v) : [...value, v]);
  return (
    <div className={cn('flex flex-wrap gap-1.5', className)}>
      {options.map((option) => {
        const active = value.includes(option.value);
        return (
          <button
            key={option.value}
            type="button"
            aria-pressed={active}
            onClick={() => toggle(option.value)}
            className={cn(
              'h-7 rounded-md border px-2.5 text-xs font-medium outline-none transition-colors focus-visible:ring-2 focus-visible:ring-ring/40',
              active
                ? 'border-primary bg-primary/5 text-foreground'
                : 'border-border text-muted-foreground hover:border-border-2 hover:text-foreground',
            )}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
