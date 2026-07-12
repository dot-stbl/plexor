import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

/**
 * SummaryPanel — липкая панель-сводка справа в create-флоу (эталон YC: панель
 * «₽/месяц» → у нас «что развернётся»: движок/ресурсы/размещение/binding).
 * Sticky под верхним баром; на узком экране падает под форму.
 */
export interface SummaryPanelProps {
  title?: string;
  children: ReactNode;
  footer?: ReactNode;
  className?: string;
}

export function SummaryPanel({ title = 'Summary', children, footer, className }: SummaryPanelProps) {
  return (
    <aside data-od-id="summary-panel" className={cn('lg:sticky lg:top-16', className)}>
      <div className="rounded-lg border border-border bg-card">
        <div className="border-b border-border px-4 py-2.5">
          <h2 className="text-sm font-medium text-foreground">{title}</h2>
        </div>
        <div className="px-4 py-3">{children}</div>
        {footer && <div className="border-t border-border px-4 py-3">{footer}</div>}
      </div>
    </aside>
  );
}

/** Строка сводки: label слева (muted) — значение справа. */
export function SummaryRow({ label, children }: { label: ReactNode; children: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-3 py-1 text-xs">
      <span className="shrink-0 text-muted-foreground">{label}</span>
      <span className="min-w-0 truncate text-right text-foreground">{children}</span>
    </div>
  );
}
