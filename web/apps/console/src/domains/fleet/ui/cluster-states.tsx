import { useTranslation } from 'react-i18next';
import { Refresh, Search, Stacks } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { Alert, AlertAction, AlertDescription, AlertTitle } from '@/shared/ui/primitives/alert';
import { Button } from '@/shared/ui/primitives/button';
import { EmptyState } from '@/shared/ui/primitives/empty-state';

/** Loading skeleton — placeholder blocks shaped like the cluster card grid. */
export function ClusterGridSkeleton() {
  return (
    <div data-od-id="clusters-grid-skeleton" className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
      {Array.from({ length: 3 }).map((_, i) => (
        <Skeleton key={i} className="h-44 w-full" />
      ))}
    </div>
  );
}

interface ClusterErrorBannerProps {
  error: unknown;
  onRetry: () => void;
}

/** Error banner with retry button. */
export function ClusterErrorBanner({ error, onRetry }: ClusterErrorBannerProps) {
  const { t } = useTranslation();
  const message = error instanceof Error ? error.message : 'Unknown error';
  return (
    <Alert variant="destructive" data-od-id="clusters-error">
      <div>
        <AlertTitle>{t('clusters.list.errorTitle')}</AlertTitle>
        <AlertDescription>{message}</AlertDescription>
      </div>
      <AlertAction>
        <Button variant="outline" size="sm" onClick={onRetry}>
          <Refresh />
          {t('common.retry')}
        </Button>
      </AlertAction>
    </Alert>
  );
}

/** Empty state — zero clusters (before any filter). Self-hosted onboarding copy. */
export function ClusterEmptyState() {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="clusters-empty"
      icon={Stacks}
      title={t('clusters.list.empty.title')}
      description={t('clusters.list.empty.description', { code: 'plx init' })}
      docsLabel={t('clusters.list.empty.docsLabel')}
      docs={[{ href: 'https://plexor.dev/docs/install', label: t('clusters.list.empty.docsLabel') }]}
    />
  );
}

interface ClusterNoResultsStateProps {
  /** Overrides the generic title when a specific filter is the culprit (e.g. "No degraded clusters"). */
  title?: string;
  onReset: () => void;
}

/** Empty state — filters returned nothing (clusters exist but none match). */
export function ClusterNoResultsState({ title, onReset }: ClusterNoResultsStateProps) {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="clusters-no-results"
      icon={Search}
      title={title ?? t('clusters.list.empty.noResults')}
      description={t('clusters.list.empty.noResultsDescription')}
      action={
        <Button variant="outline" size="sm" onClick={onReset}>
          {t('clusters.list.empty.reset')}
        </Button>
      }
    />
  );
}
