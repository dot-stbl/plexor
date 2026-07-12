import { useState, type ReactNode } from 'react';
import { CaretRight } from '@/shared/ui/icon';
import { cn } from '@/lib/utils';

/**
 * Disclosure — лёгкий self-toggle раскрывающийся блок (эталон YC/Proxmox
 * «Advanced»). НЕ `Accordion`: контент рендерится условно, поэтому контейнер
 * динамического размера — нет фиксированной высоты панели и хвостового
 * отступа в свёрнутом виде. Дети обычно `FieldRow` (внутри — `divide-y`).
 *
 * Используем для «продвинутых» knob'ов форм: базовые поля видны всегда, глубина
 * (self-hosted) — по клику. См. rule про формирование параметров.
 */
export interface DisclosureProps {
  /** Текст-триггер (напр. «Advanced · CPU type, NUMA»). */
  summary: ReactNode;
  children: ReactNode;
  defaultOpen?: boolean;
  className?: string;
}

export function Disclosure({ summary, children, defaultOpen = false, className }: DisclosureProps) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className={cn('py-2', className)}>
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        className="flex items-center gap-1 text-xs font-medium text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:text-foreground"
      >
        <CaretRight className={`size-3.5 transition-transform ${open ? 'rotate-90' : ''}`} />
        {summary}
      </button>
      {open && <div className="mt-2 divide-y divide-border border-t border-border">{children}</div>}
    </div>
  );
}
