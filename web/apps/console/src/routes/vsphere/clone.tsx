import { createFileRoute, Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ArrowBack } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { PageTemplate } from '@/shared/ui/app-shell';
import { routeHead } from '@/shared/lib/route-head';
import { VSphereCloneForm } from '@/features/vsphere';

export const Route = createFileRoute('/vsphere/clone')({
  staticData: { crumb: 'Clone VM from vSphere template' },
  component: VSphereClonePage,
  ...routeHead('Clone vSphere VM'),
});

/** Thin wrapper around the clone form — adds the page header + back link. */
function VSphereClonePage() {
  const { t } = useTranslation();
  return (
    <PageTemplate
      data-od-id="vsphere-clone-page"
      width="3xl"
      title={t('vsphere.clone.title')}
      description={t('vsphere.clone.description')}
      actions={
        <Button variant="ghost" nativeButton={false} render={<Link to="/vsphere" />}>
          <ArrowBack />
          {t('common.back')}
        </Button>
      }
    >
      <VSphereCloneForm />
    </PageTemplate>
  );
}
