import { useTranslation } from 'react-i18next';
import { ArrowBack } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Alert, AlertDescription, AlertTitle } from '@/shared/ui/primitives/alert';
import { Button } from '@/shared/ui/primitives/button';
import { Skeleton } from '@/shared/ui/primitives/skeleton';

/**
 * /lxc/$id auxiliary states.
 *
 * `LxcDetailSkeleton` is the loading seam: the handmade mock is
 * synchronous, so the route never shows it today — it exists so the
 * kubb-driven page (and the stories) already have the pending shape
 * once GET /lxc/containers/{id} lands in the contract.
 */
export function LxcDetailSkeleton() {
  const { t } = useTranslation();
  return (
    <PageTemplate
      data-od-id="lxc-detail-skeleton"
      title={t('lxc.detail.title')}
      width="wide"
      actions={
        <Button variant="ghost">
          <ArrowBack />
          {t('lxc.detail.back')}
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

/** Unknown id — the 404 shape for /lxc/$id. */
export function LxcDetailNotFound({ id, onBack }: { id: string; onBack: () => void }) {
  const { t } = useTranslation();
  return (
    <PageTemplate
      data-od-id="lxc-detail-not-found"
      title={t('lxc.detail.notFound')}
      width="default"
      actions={
        <Button variant="ghost" onClick={onBack}>
          <ArrowBack />
          {t('lxc.detail.back')}
        </Button>
      }
    >
      <Alert variant="destructive">
        <AlertTitle>{t('lxc.detail.notFound')}</AlertTitle>
        <AlertDescription>{t('lxc.detail.notFoundDescription', { id })}</AlertDescription>
      </Alert>
    </PageTemplate>
  );
}
