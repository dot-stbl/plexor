import { useTranslation } from 'react-i18next';
import { Search } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { Skeleton } from '@/shared/ui/primitives/skeleton';

/** Loading skeleton — 5 placeholder rows shaped like the container table. */
export function LxcSkeleton() {
  return (
    <div data-od-id="lxc-skeleton" className="flex flex-col gap-2">
      {Array.from({ length: 5 }).map((_, index) => (
        <Skeleton key={index} className="h-10 w-full" />
      ))}
    </div>
  );
}

interface LxcNoResultsStateProps {
  /** Overrides the generic title when a specific filter is the culprit (e.g. "No paused containers"). */
  title?: string;
  onReset: () => void;
}

/** Empty state — filters returned nothing (containers exist but none match). */
export function LxcNoResultsState({ title, onReset }: LxcNoResultsStateProps) {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="lxc-no-results"
      icon={Search}
      title={title ?? t('lxc.list.empty.noResults')}
      description={t('lxc.list.empty.noResultsDescription')}
      action={
        <Button variant="outline" size="sm" onClick={onReset}>
          {t('lxc.list.empty.reset')}
        </Button>
      }
    />
  );
}
