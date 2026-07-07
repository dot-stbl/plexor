import { createFileRoute } from '@tanstack/react-router';
import { Receipt } from '@phosphor-icons/react';
import { PlaceholderPage } from '@/shared/ui/app-shell';

export const Route = createFileRoute('/billing')({
  component: BillingPage,
});

function BillingPage() {
  return (
    <PlaceholderPage
      eyebrow="Управление · Финансы"
      title="Расходы"
      description="Биллинг и потребление ресурсов по проекту."
      icon={Receipt}
    />
  );
}
