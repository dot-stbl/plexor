import { useTranslation } from 'react-i18next';
import { Search } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { Skeleton } from '@/shared/ui/primitives/skeleton';

/** Loading skeleton — 5 placeholder rows shaped like the cluster table. */
export function K8sSkeleton() {
  return (
    <div data-od-id="k8s-skeleton" className="flex flex-col gap-2">
      {Array.from({ length: 5 }).map((_, index) => (
        <Skeleton key={index} className="h-10 w-full" />
      ))}
    </div>
  );
}

interface K8sNoResultsStateProps {
  /** Overrides the generic title when a specific filter is the culprit (e.g. "No degraded clusters"). */
  title?: string;
  onReset: () => void;
}

/** Empty state — filters returned nothing (clusters exist but none match). */
export function K8sNoResultsState({ title, onReset }: K8sNoResultsStateProps) {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="k8s-no-results"
      icon={Search}
      title={title ?? t('k8s.list.empty.noResults')}
      description={t('k8s.list.empty.noResultsDescription')}
      action={
        <Button variant="outline" size="sm" onClick={onReset}>
          {t('k8s.list.empty.reset')}
        </Button>
      }
    />
  );
}
