import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

/**
 * PageTemplate — каркас страницы (замена per-page PageHeader). Владеет чромом:
 * заголовок + actions + контент-контейнер (max-width/padding). Страница передаёт
 * только title/actions/children — не повторяет `mx-auto max-w-* px-* py-*`.
 *
 * Хлебные крошки тут НЕ рендерятся — они в верхнем баре (AppHeader) из
 * route-matches (web-frontend.md rule 57). Открывается через `<Outlet/>`
 * layout-роута (rule 58).
 *
 * Default `width="default"` (centered, max-w-6xl). Страницы, которым нужно
 * растянуться до max-w-7xl для data tables / list-detail layouts, передают
 * `width="wide"` явно.
 */
export interface PageTemplateProps {
  title: string;
  description?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
  /** Ширина контента. `default` — центрированный (default, max-w-6xl);
   *  `narrow` — узкие формы (max-w-3xl); `wide` — широкие списки/таблицы (max-w-7xl). */
  width?: 'default' | 'narrow' | 'wide';
  'data-od-id'?: string;
}

const WIDTH = {
  narrow: 'max-w-3xl',
  default: 'max-w-6xl',
  wide: 'max-w-7xl',
} as const;

export function PageTemplate({
  title,
  description,
  actions,
  children,
  width = 'default',
  ...props
}: PageTemplateProps) {
  const max = WIDTH[width];
  return (
    <main data-od-id={props['data-od-id']}>
      <header className="border-b border-border">
        <div className={cn('mx-auto flex w-full items-start justify-between gap-3 px-6 py-5 lg:px-8', max)}>
          <div className="min-w-0 space-y-1">
            <h1 className="truncate text-xl font-semibold tracking-tight">{title}</h1>
            {description && (
              <div className="max-w-prose text-sm text-muted-foreground [&_button]:inline-flex">
                {description}
              </div>
            )}
          </div>
          {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
        </div>
      </header>
      <div className={cn('mx-auto w-full px-6 py-6 lg:px-8', max)}>{children}</div>
    </main>
  );
}
