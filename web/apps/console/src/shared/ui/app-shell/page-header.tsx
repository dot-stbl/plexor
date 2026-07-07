import type { ReactNode } from 'react';

/**
 * Shared page header template used by every route: a section breadcrumb, the
 * page title + description, and the page's base actions. The current
 * project/folder scope is not repeated here — it lives in the persistent top
 * bar above (always visible across pages).
 */
export function PageHeader({
  breadcrumb,
  title,
  description,
  actions,
}: {
  breadcrumb?: string[];
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <header className="border-b border-border" data-od-id="page-header">
      <div className="mx-auto w-full max-w-6xl px-6 py-5 lg:px-8">
        {breadcrumb && breadcrumb.length > 0 && (
          <nav
            aria-label="Хлебные крошки"
            className="mb-2 flex items-center gap-1.5 font-mono text-[11px] uppercase tracking-[0.06em] text-muted-foreground"
          >
            {breadcrumb.map((crumb, index) => (
              <span key={crumb} className="flex items-center gap-1.5">
                {index > 0 && <span className="text-muted-foreground/50">/</span>}
                <span>{crumb}</span>
              </span>
            ))}
          </nav>
        )}
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0 space-y-1">
            <h1 className="text-xl font-semibold tracking-tight">{title}</h1>
            {description && <p className="max-w-prose text-sm text-muted-foreground">{description}</p>}
          </div>
          {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
        </div>
      </div>
    </header>
  );
}
