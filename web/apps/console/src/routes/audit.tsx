import { createFileRoute } from '@tanstack/react-router';
import { FilterAlt, History } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useTranslation } from 'react-i18next';
import { PlaceholderPage } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/audit')({
  component: AuditPage,
  ...routeHead('Audit'),
});

function AuditPage() {
  const { t } = useTranslation();
  return (
    <PlaceholderPage
      title={t('audit.title')}
      description={t('audit.description')}
      icon={History}
      actions={
        <Button variant="outline">
          <FilterAlt className="size-4" />
          {t('audit.filter')}
        </Button>
      }
    />
  );
}
