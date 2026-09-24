import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { Alert, AlertTitle, AlertDescription, AlertAction } from '@/shared/ui/primitives/alert';
import { Button } from '@/shared/ui/primitives/button';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { DeployedCode, Refresh } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useTranslation } from 'react-i18next';

/** Loading skeleton — 3 placeholder blocks for the three resource tables. */
export function VSphereInventorySkeleton() {
  return (
    <div data-od-id="vsphere-inventory-skeleton" className="flex flex-col gap-6">
      {[0, 1, 2].map((index) => (
        <div key={index} className="flex flex-col gap-2">
          <Skeleton className="h-5 w-40" />
          <Skeleton className="h-32 w-full" />
        </div>
      ))}
    </div>
  );
}

interface VSphereErrorBannerProps {
  error: unknown;
  onRetry: () => void;
}

/**
 * Error banner with retry. Carries the title "vSphere is not configured"
 * vs the inventory-empty 503 specifically (the 503 response is the
 * same code in both cases — the body differs in `title`).
 */
export function VSphereErrorBanner({ error, onRetry }: VSphereErrorBannerProps) {
  const { t } = useTranslation();
  const title = error instanceof Error ? error.message : t('vsphere.inventory.errorTitle');
  return (
    <Alert variant="destructive" data-od-id="vsphere-inventory-error">
      <div>
        <AlertTitle>{t('vsphere.inventory.errorTitle')}</AlertTitle>
        <AlertDescription>{title}</AlertDescription>
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

interface VSphereEmptyStateProps {
  onRefresh?: () => void;
}

/**
 * Empty state — the inventory is reachable but no snapshot has
 * ever been pulled (GET returns 503). The caller surfaces a
 * primary "Refresh now" button so the user can kick off the first
 * pull without leaving the page.
 */
export function VSphereEmptyState({ onRefresh }: VSphereEmptyStateProps) {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="vsphere-inventory-empty"
      icon={DeployedCode}
      title={t('vsphere.inventory.empty.title')}
      description={t('vsphere.inventory.empty.description')}
      action={
        onRefresh ? (
          <Button onClick={onRefresh}>
            <Refresh />
            {t('vsphere.inventory.refresh')}
          </Button>
        ) : undefined
      }
    />
  );
}
