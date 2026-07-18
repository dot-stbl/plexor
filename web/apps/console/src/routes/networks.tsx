import { createFileRoute } from '@tanstack/react-router';
import { AccountTree, Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useTranslation } from 'react-i18next';
import { PlaceholderPage } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/networks')({
  component: NetworksPage,
  ...routeHead('Networks'),
});

function NetworksPage() {
  const { t } = useTranslation();
  return (
    <PlaceholderPage
      title={t('networks.title')}
      description={t('networks.description')}
      icon={AccountTree}
      actions={
        <Button>
          <Add className="size-4" />
          {t('networks.create')}
        </Button>
      }
    />
  );
}
