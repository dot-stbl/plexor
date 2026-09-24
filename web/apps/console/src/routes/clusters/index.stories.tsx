import type { Meta, StoryObj } from '@storybook/react-vite';
import { useTranslation } from 'react-i18next';
import { MenuBook } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import {
  ClusterCard,
  ClusterListBody,
  ClusterNoResultsState,
  ClusterStatusStrip,
  clusterHealth,
  countByHealthFacet,
  sumFleetTotals,
  type ClusterHealth,
  type PlexorCluster,
  type PlexorNode,
} from '@/domains/fleet';

/**
 * /clusters list page stories.
 *
 * Renders `ClusterListBody` (the whole page body incl. the health strip)
 * with deterministic fixture fleets so baselines never depend on mock
 * state. `HealthChipActive` and `NoResults` render the controlled strip +
 * grid directly — the body owns its filter state internally, so pre-set
 * filter states are shown through the strip's controlled props instead.
 */

const node = (overrides: Partial<PlexorNode> & Pick<PlexorNode, 'id' | 'hostname'>): PlexorNode => ({
  role: 'compute',
  status: 'ready',
  spec: { vcpu: 16, ramGb: 64, diskGb: 2000, providers: ['kvm'] },
  isoVersion: 'plexor-1.2.3',
  joinedAt: '2026-05-20T11:00:00Z',
  lastSeenAt: '2026-09-19T09:55:00Z',
  vmCount: 2,
  ...overrides,
});

const FLEET: PlexorCluster[] = [
  {
    id: 'cluster-prod-eu-1',
    name: 'prod-eu-1',
    installProviders: ['kvm', 'ceph-rbd', 'ovs', 'ceph-rgw', 'postgresql'],
    hostVersion: '1.2.3',
    uptimeSeconds: 14 * 24 * 3600 + 2 * 3600,
    endpoint: 'https://prod-eu-1.plexor.local:8443',
    createdAt: '2026-03-01T08:00:00Z',
    nodes: [
      node({ id: 'node-r1n1', hostname: 'rack1-n01', role: 'control' }),
      node({ id: 'node-r1n2', hostname: 'rack1-n02' }),
      node({ id: 'node-r2n1', hostname: 'rack2-n01', status: 'draining', vmCount: 0 }),
    ],
    tokens: [],
  },
  {
    id: 'cluster-staging-eu',
    name: 'staging-eu',
    installProviders: ['kvm', 'lvm-thin', 'ovs'],
    hostVersion: '1.2.2',
    uptimeSeconds: 3 * 3600 + 12 * 60,
    endpoint: 'https://staging-eu.plexor.local:8443',
    createdAt: '2026-06-10T08:00:00Z',
    nodes: [
      node({ id: 'node-stg1', hostname: 'stg-n01', role: 'control' }),
      node({ id: 'node-stg2', hostname: 'stg-n02' }),
    ],
    tokens: [],
  },
  {
    id: 'cluster-edge-ams',
    name: 'edge-ams',
    installProviders: ['kvm'],
    hostVersion: '1.2.0',
    uptimeSeconds: 45 * 60,
    endpoint: 'https://edge-ams.plexor.local:8443',
    createdAt: '2026-08-02T08:00:00Z',
    nodes: [
      node({ id: 'node-ams1', hostname: 'edge-ams-01', role: 'control', status: 'offline', vmCount: 0 }),
      node({ id: 'node-ams2', hostname: 'edge-ams-02', status: 'offline', vmCount: 0 }),
      node({ id: 'node-ams3', hostname: 'edge-ams-03' }),
    ],
    tokens: [],
  },
];

const noop = () => {};

/** Shared page frame for the controlled-strip stories (mirrors ClusterListBody's). */
function StripStoryFrame({
  fleet = FLEET,
  activeHealth,
  children,
}: {
  fleet?: PlexorCluster[];
  activeHealth: ClusterHealth | null;
  children?: React.ReactNode;
}) {
  const { t } = useTranslation();
  const counts = countByHealthFacet(fleet);
  const visible = activeHealth
    ? fleet.filter((cluster) => clusterHealth(cluster.nodes) === activeHealth)
    : fleet;

  return (
    <PageTemplate
      data-od-id="clusters-list"
      title={t('clusters.list.title')}
      width="wide"
      description={
        <span>
          <MonoNum>{sumFleetTotals(fleet).ready}</MonoNum>
          <span className="text-muted-foreground">/</span>
          <MonoNum>{sumFleetTotals(fleet).nodes}</MonoNum>{' '}
          <span className="text-muted-foreground">{t('clusters.list.nodesReady')}</span>
        </span>
      }
      actions={
        <Button>
          <MenuBook />
          {t('clusters.list.docs')}
        </Button>
      }
    >
      <div className="space-y-2">
        <ClusterStatusStrip
          counts={counts}
          activeHealth={activeHealth}
          onToggleHealth={noop}
          totals={sumFleetTotals(visible)}
        />
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
          {visible.map((cluster) => (
            <ClusterCard key={cluster.id} cluster={cluster} onOpen={noop} />
          ))}
        </div>
        {children}
      </div>
    </PageTemplate>
  );
}

/** Grid filtered to the active chip — same composition the body produces. */
function HealthChipActiveBody() {
  return <StripStoryFrame activeHealth="down" />;
}

/** Active chip on a health with zero matches — strip + no-results state. */
function NoResultsBody() {
  const { t } = useTranslation();
  return (
    <StripStoryFrame fleet={FLEET.slice(0, 2)} activeHealth="down">
      <ClusterNoResultsState
        title={t('clusters.list.empty.noResultsHealthTitle', { status: t('clusters.health.down') })}
        onReset={noop}
      />
    </StripStoryFrame>
  );
}

const meta = {
  title: 'Pages/ClusterList',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: fleet with mixed health, strip chips + search + card grid. */
export const Default: Story = {
  render: () => <ClusterListBody clusters={FLEET} onOpenCluster={noop} />,
};

/** Loading: strip skeleton + card skeletons. */
export const Loading: Story = {
  render: () => <ClusterListBody clusters={[]} onOpenCluster={noop} isPending />,
};

/** Empty fleet: no strip, self-hosted onboarding empty state. */
export const Empty: Story = {
  render: () => <ClusterListBody clusters={[]} onOpenCluster={noop} />,
};

/** One chip active: down chip emphasized, grid filtered to the down cluster. */
export const HealthChipActive: Story = {
  render: () => <HealthChipActiveBody />,
};

/** No results: down chip active with zero down clusters — status-aware empty state. */
export const NoResults: Story = {
  render: () => <NoResultsBody />,
};
