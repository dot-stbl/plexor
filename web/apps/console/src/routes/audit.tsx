import { createFileRoute } from '@tanstack/react-router';
import { ClockCounterClockwise, Funnel } from '@phosphor-icons/react';
import { PlaceholderPage } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';

export const Route = createFileRoute('/audit')({
  component: AuditPage,
});

function AuditPage() {
  return (
    <PlaceholderPage
      breadcrumb={['Управление', 'Безопасность']}
      title="Журнал аудита"
      description="История действий пользователей и системы в проекте."
      icon={ClockCounterClockwise}
      actions={
        <Button variant="outline" size="sm">
          <Funnel className="size-4" />
          Фильтр
        </Button>
      }
    />
  );
}
