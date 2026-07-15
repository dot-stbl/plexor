import type { ReactNode } from 'react';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { cn } from '@/lib/utils';

/**
 * SummaryPanel — липкая панель-сводка справа в create-флоу (эталон YC: «₽/месяц»
 * → у нас «что развернётся»). Построена на shadcn `Card` (не сырые div'ы):
 * заголовок — `CardHeader`, строки — `CardContent`, бейджи/флаги — `CardFooter`
 * (передаются через `footer`). На узком экране падает под форму.
 */
export interface SummaryPanelProps {
  title?: string;
  children: ReactNode;
  /** Нижняя секция (напр. флаги-бейджи) — рендерится в `CardFooter`. */
  footer?: ReactNode;
  className?: string;
}

export function SummaryPanel({ title = 'Summary', children, footer, className }: SummaryPanelProps) {
  return (
    <aside data-od-id="summary-panel" className={cn('lg:sticky lg:top-16 lg:self-start', className)}>
      <Card size="sm">
        <CardHeader className="border-b border-border">
          <CardTitle className="text-sm">{title}</CardTitle>
        </CardHeader>
        <CardContent>{children}</CardContent>
        {footer && <CardFooter className="border-t border-border">{footer}</CardFooter>}
      </Card>
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
