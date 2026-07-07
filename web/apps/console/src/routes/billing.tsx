import { createFileRoute } from '@tanstack/react-router';
import { Receipt, DownloadSimple } from '@phosphor-icons/react';
import { PlaceholderPage } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';

export const Route = createFileRoute('/billing')({
  component: BillingPage,
});

function BillingPage() {
  return (
    <PlaceholderPage
      breadcrumb={['Управление', 'Финансы']}
      title="Расходы"
      description="Биллинг и потребление ресурсов по проекту."
      icon={Receipt}
      actions={
        <Button variant="outline" size="sm">
          <DownloadSimple className="size-4" />
          Экспорт
        </Button>
      }
    />
  );
}
