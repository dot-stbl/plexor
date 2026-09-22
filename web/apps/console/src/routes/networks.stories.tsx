import type { Meta, StoryObj } from '@storybook/react-vite';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { AccountTree } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { DataTable } from '@/shared/ui/data-table';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { getNetworkColumns } from '@/features/networks';
import { listNetworks, type Network } from '@/shared/api/mocks/handmade/networks';

/**
 * /networks page stories.
 *
 * Two states:
 *   Default: the realistic default — three active VPCs (prod-eu, staging-eu,
 *            dev-eu). Table renders the VPC inventory.
 *   Empty: zero VPCs → the EmptyState shell without the router CTA (the real
 *          page's NetworksEmpty wires the CTA to /networks, which would
 *          self-navigate and crash the test-runner's smoke test).
 *
 * The route reads via `useNetworks()`; stories use the same mock factory
 * directly so the snapshots are deterministic across machines.
 */

function NetworksPageBody({ networks }: { networks: Network[] }) {
  const { t } = useTranslation();
  const columns = useMemo(() => getNetworkColumns(t), [t]);
  const activeCount = networks.filter((n) => n.status === 'active').length;
  const totalSubnets = networks.reduce((acc, n) => acc + n.subnetCount, 0);
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
        <EmptyState
          data-od-id="networks-empty"
          icon={AccountTree}
          title={t('networks.empty.title')}
          description={t('networks.empty.description')}
          docs={[
            { href: 'https://plexor.dev/docs/networking/vpc', label: t('networks.empty.docs.vpc') },
            { href: 'https://plexor.dev/docs/networking/subnets', label: t('networks.empty.docs.subnets') },
            { href: 'https://plexor.dev/docs/networking/security-groups', label: t('networks.empty.docs.sg') },
          ]}
        />
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

const meta = {
  title: 'Pages/Networks',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: realistic first-run state (3 VPCs across prod/staging/dev). */
export const Default: Story = {
  render: () => <NetworksPageBody networks={listNetworks()} />,
};

/** Empty: zero VPCs → the empty shell. */
export const Empty: Story = {
  render: () => <NetworksPageBody networks={[]} />,
};