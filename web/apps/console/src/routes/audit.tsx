import { createFileRoute } from '@tanstack/react-router';
import { FilterAlt, History } from '@nine-thirty-five/material-symbols-react/rounded/700';
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
      icon={History}
      actions={
        <Button variant="outline" size="sm">
          <FilterAlt className="size-4" />
          Фильтр
        </Button>
      }
    />
  );
}
