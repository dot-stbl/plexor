import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { Alert, AlertTitle, AlertDescription, AlertAction } from '@/shared/ui/primitives/alert';
import { Button } from '@/shared/ui/primitives/button';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { DeployedCode, Refresh, Search } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useTranslation } from 'react-i18next';

/** Loading skeleton — 5 placeholder rows shaped like the VM table. */
export function VmSkeleton() {
  return (
    <div data-od-id="vms-skeleton" className="flex flex-col gap-2">
      {Array.from({ length: 5 }).map((_, i) => (
        <Skeleton key={i} className="h-10 w-full" />
      ))}
    </div>
  );
}

interface VmErrorBannerProps {
  error: unknown;
  onRetry: () => void;
}

/** Error banner with retry button. */
export function VmErrorBanner({ error, onRetry }: VmErrorBannerProps) {
  const { t } = useTranslation();
  const message = error instanceof Error ? error.message : 'Unknown error';
  return (
    <Alert variant="destructive" data-od-id="vms-error">
      <div>
        <AlertTitle>{t('vms.list.errorTitle')}</AlertTitle>
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

interface VmEmptyStateProps {
  onCreate?: () => void;
}

/** Empty state — zero VMs in the project (before any filter). */
export function VmEmptyState({ onCreate }: VmEmptyStateProps) {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="vms-empty"
      icon={DeployedCode}
      title={t('vms.list.empty.title')}
      description={t('vms.list.empty.description')}
      action={onCreate ? <Button onClick={onCreate}>{t('vms.list.create')}</Button> : undefined}
    />
  );
}

interface VmNoResultsStateProps {
  onReset: () => void;
}

/** Empty state — filters returned nothing (VMs exist but none match). */
export function VmNoResultsState({ onReset }: VmNoResultsStateProps) {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="vms-no-results"
      icon={Search}
      title={t('vms.list.empty.noResults')}
      description={t('vms.list.empty.noResultsDescription')}
      action={<Button variant="outline" size="sm" onClick={onReset}>{t('vms.list.empty.reset')}</Button>}
    />
  );
}