import { createFileRoute } from '@tanstack/react-router';
import { Cube, Plus } from '@phosphor-icons/react';
import { PlaceholderPage } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';

export const Route = createFileRoute('/vms')({
  component: VmsPage,
});

function VmsPage() {
  return (
    <PlaceholderPage
      breadcrumb={['Ресурсы', 'Вычисления']}
      title="Виртуальные машины"
      description="Здесь появится список инстансов на данных из useListVms() — MSW-моки уже готовы."
      icon={Cube}
      actions={
        <Button size="sm">
          <Plus className="size-4" />
          Создать ВМ
        </Button>
      }
    />
  );
}
