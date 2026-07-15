import { createFileRoute } from '@tanstack/react-router';
import { Download, Receipt } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useTranslation } from 'react-i18next';
import { PlaceholderPage } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';

export const Route = createFileRoute('/billing')({
  component: BillingPage,
});

function BillingPage() {
  const { t } = useTranslation();
  return (
    <PlaceholderPage
      title={t('billing.title')}
      description={t('billing.description')}
      icon={Receipt}
      actions={
        <Button variant="outline">
          <Download className="size-4" />
          {t('billing.export')}
        </Button>
      }
    />
  );
}
