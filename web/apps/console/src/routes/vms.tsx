import { createFileRoute } from '@tanstack/react-router';
import { Cube } from '@phosphor-icons/react';
import { PlaceholderPage } from '@/shared/ui/app-shell';

export const Route = createFileRoute('/vms')({
  component: VmsPage,
});

function VmsPage() {
  return (
    <PlaceholderPage
      eyebrow="Ресурсы · Compute"
      title="Виртуальные машины"
      description="Здесь появится список инстансов на данных из useListVms() — MSW-моки уже готовы."
      icon={Cube}
    />
  );
}
