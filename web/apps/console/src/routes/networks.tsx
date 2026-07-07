import { createFileRoute } from '@tanstack/react-router';
import { TreeStructure } from '@phosphor-icons/react';
import { PlaceholderPage } from '@/shared/ui/app-shell';

export const Route = createFileRoute('/networks')({
  component: NetworksPage,
});

function NetworksPage() {
  return (
    <PlaceholderPage
      eyebrow="Ресурсы · Сеть"
      title="Сети и VPC"
      description="VPC, подсети и security groups проекта."
      icon={TreeStructure}
    />
  );
}
