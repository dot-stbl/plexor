import { createFileRoute } from '@tanstack/react-router';
import { Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useTranslation } from 'react-i18next';
import { useMemo } from 'react';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { DataTable } from '@/shared/ui/data-table';
import { getNetworkColumns, useNetworks, NetworksEmpty } from '@/features/networks';
import { routeHead } from '@/shared/lib/route-head';

/**
 * /networks — read-only projection of VPC inventory. Kubb contract for
 * /api/v1/networks hasn't landed yet (Phase X), so the page reads from
 * the handmade mock at `shared/api/mocks/handmade/networks.ts`. The hook
 * signature is the same one we'll wire when the API is ready — migration
 * is one file.
 */
export const Route = createFileRoute('/networks')({
  component: NetworksPage,
  ...routeHead('Networks'),
});

function NetworksPage() {
  const { t } = useTranslation();
  const { networks, activeCount, totalSubnets } = useNetworks();
  const columns = useMemo(() => getNetworkColumns(t), [t]);

  return (
    <PageTemplate
      title={t('networks.title')}
      description={t('networks.description')}
      width="wide"
      data-od-id="networks"
      actions={
        networks.length > 0 ? (
          <Button>
            <Add className="size-3.5" />
            {t('networks.create')}
          </Button>
        ) : null
      }
    >
      {networks.length === 0 ? (
        <NetworksEmpty />
      ) : (
        <div className="space-y-3">
          <p className="text-xs text-muted-foreground">
            {t('networks.summary', {
              networks: networks.length,
              subnets: totalSubnets,
              active: activeCount,
            })}
          </p>
          <DataTable columns={columns} data={networks} density="compact" />
        </div>
      )}
    </PageTemplate>
  );
}
