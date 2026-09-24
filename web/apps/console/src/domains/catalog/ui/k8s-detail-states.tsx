import { useTranslation } from 'react-i18next';
import { ArrowBack } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Alert, AlertDescription, AlertTitle } from '@/shared/ui/primitives/alert';
import { Button } from '@/shared/ui/primitives/button';
import { Skeleton } from '@/shared/ui/primitives/skeleton';

/**
 * /k8s/$id auxiliary states.
 *
 * `K8sDetailSkeleton` is the loading seam: the handmade mock is
 * synchronous, so the route never shows it today — it exists so the
 * kubb-driven page (and the stories) already have the pending shape
 * once GET /k8s/{id} lands in the contract.
 */
export function K8sDetailSkeleton() {
  const { t } = useTranslation();
  return (
    <PageTemplate
      data-od-id="k8s-detail-skeleton"
      title={t('k8s.detail.title')}
      width="wide"
      actions={
        <Button variant="ghost">
          <ArrowBack />
          {t('k8s.detail.back')}
        </Button>
      }
    >
      <div className="flex flex-col gap-2">
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-20 w-full" />
      </div>
    </PageTemplate>
  );
}

/** Unknown id — the 404 shape for /k8s/$id. */
export function K8sDetailNotFound({ id, onBack }: { id: string; onBack: () => void }) {
  const { t } = useTranslation();
  return (
    <PageTemplate
      data-od-id="k8s-detail-not-found"
      title={t('k8s.detail.notFound')}
      width="default"
      actions={
        <Button variant="ghost" onClick={onBack}>
          <ArrowBack />
          {t('k8s.detail.back')}
        </Button>
      }
    >
      <Alert variant="destructive">
        <AlertTitle>{t('k8s.detail.notFound')}</AlertTitle>
        <AlertDescription>{t('k8s.detail.notFoundDescription', { id })}</AlertDescription>
      </Alert>
    </PageTemplate>
  );
}
