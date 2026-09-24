import { createFileRoute } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { routeHead } from '@/shared/lib/route-head';
import { ImageListBody, listImages } from '@/domains/compute';

export const Route = createFileRoute('/images')({
  staticData: { crumb: 'Images' },
  component: ImagesPage,
  ...routeHead('Images'),
});

/**
 * Image catalog — global (cross-section) resource: disk templates for VM
 * provisioning. Thin data shell; strip + filtering + selection live in
 * `ImageListBody` (client-side `applyFilters`).
 */
function ImagesPage() {
  const { t } = useTranslation();

  return (
    <ImageListBody items={listImages()} onCreate={() => toast(t('images.list.createToast'))} />
  );
}
