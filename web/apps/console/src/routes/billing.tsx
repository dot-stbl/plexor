import { createFileRoute } from '@tanstack/react-router';
import { Download, Receipt } from '@nine-thirty-five/material-symbols-react/rounded/700';
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
          <Download className="size-4" />
          Экспорт
        </Button>
      }
    />
  );
}
