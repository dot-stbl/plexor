import type { ReactNode } from 'react';

/**
 * Shared page header used by every route: title + description + base actions.
 * Breadcrumbs now live in the top bar (AppHeader); the `breadcrumb` prop is
 * accepted for compatibility but no longer rendered here.
 */
export function PageHeader({
  title,
  description,
  actions,
}: {
  breadcrumb?: string[];
  title: string;
  description?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <header className="border-b border-border" data-od-id="page-header">
      <div className="mx-auto w-full max-w-6xl px-6 py-5 lg:px-8">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0 space-y-1">
            <h1 className="text-xl font-semibold tracking-tight">{title}</h1>
            {description && (
              <div className="max-w-prose text-sm text-muted-foreground [&_button]:inline-flex">
                {description}
              </div>
            )}
          </div>
          {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
        </div>
      </div>
    </header>
  );
}
