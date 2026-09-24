import type { Icon } from '@nine-thirty-five/material-symbols-react';
import { useTranslation } from 'react-i18next';
import { PageTemplate } from './page-template';

/**
 * Honest placeholder for a route whose real content is not built yet.
 * Wraps `PageTemplate` (consistent chrome: title + description + actions)
 * with a dashed empty-state panel as the body.
 *
 * Inherits `PageTemplate`'s default width (centered, max-w-6xl).
 */
export function PlaceholderPage({
  title,
  description,
  icon: PageIcon,
  actions,
  width,
}: {
  title: string;
  description: string;
  icon: Icon;
  actions?: React.ReactNode;
  width?: 'default' | 'narrow' | 'wide';
}) {
  const { t } = useTranslation();
  return (
    <PageTemplate
      title={title}
      description={description}
      actions={actions}
      width={width}
    >
      <div className="flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-border py-16 text-center">
        <div className="flex size-11 items-center justify-center rounded-md border border-border bg-muted/40 text-muted-foreground">
          <PageIcon className="size-5" />
        </div>
        <div className="space-y-1">
          <p className="text-sm font-medium">{t('placeholder.inDevelopment')}</p>
          <p className="text-xs text-muted-foreground">
            {t('placeholder.scaffoldReady')}
          </p>
        </div>
      </div>
    </PageTemplate>
  );
}
