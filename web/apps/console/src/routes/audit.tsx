import { createFileRoute } from '@tanstack/react-router';
import { ClockCounterClockwise } from '@phosphor-icons/react';
import { PlaceholderPage } from '@/shared/ui/app-shell';

export const Route = createFileRoute('/audit')({
  component: AuditPage,
});

function AuditPage() {
  return (
    <PlaceholderPage
      eyebrow="Управление · Безопасность"
      title="Журнал аудита"
      description="История действий пользователей и системы в проекте."
      icon={ClockCounterClockwise}
    />
  );
}
