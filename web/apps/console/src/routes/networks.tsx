import { createFileRoute } from '@tanstack/react-router';
import { TreeStructure, Plus } from '@phosphor-icons/react';
import { PlaceholderPage } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';

export const Route = createFileRoute('/networks')({
  component: NetworksPage,
});

function NetworksPage() {
  return (
    <PlaceholderPage
      breadcrumb={['Ресурсы', 'Сеть']}
      title="Сети и VPC"
      description="VPC, подсети и security groups проекта."
      icon={TreeStructure}
      actions={
        <Button size="sm">
          <Plus className="size-4" />
          Создать сеть
        </Button>
      }
    />
  );
}
