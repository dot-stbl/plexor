import type { Icon } from '@phosphor-icons/react';
import type { ReactNode } from 'react';
import { PageHeader } from './page-header';

/**
 * Honest placeholder for a route whose real content is not built yet.
 * Uses the shared PageHeader template + a dashed empty-state panel.
 */
export function PlaceholderPage({
  breadcrumb,
  title,
  description,
  icon: PageIcon,
  actions,
}: {
  breadcrumb?: string[];
  title: string;
  description: string;
  icon: Icon;
  actions?: ReactNode;
}) {
  return (
    <>
      <PageHeader breadcrumb={breadcrumb} title={title} description={description} actions={actions} />
      <main className="mx-auto w-full max-w-6xl px-6 py-8 lg:px-8">
        <div className="flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-border py-16 text-center">
          <div className="flex size-11 items-center justify-center rounded-md border border-border bg-muted/40 text-muted-foreground">
            <PageIcon className="size-5" />
          </div>
          <div className="space-y-1">
            <p className="text-sm font-medium">Экран в разработке</p>
            <p className="text-xs text-muted-foreground">
              Каркас готов — контент появится в следующем проходе.
            </p>
          </div>
        </div>
      </main>
    </>
  );
}
