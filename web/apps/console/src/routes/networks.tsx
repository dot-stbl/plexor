import { createFileRoute } from '@tanstack/react-router';
import { AccountTree, Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PlaceholderPage } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';

export const Route = createFileRoute('/networks')({
  component: NetworksPage,
});

function NetworksPage() {
  return (
    <PlaceholderPage
      title="Сети и VPC"
      description="VPC, подсети и security groups проекта."
      icon={AccountTree}
      actions={
        <Button>
          <Add className="size-4" />
          Создать сеть
        </Button>
      }
    />
  );
}
