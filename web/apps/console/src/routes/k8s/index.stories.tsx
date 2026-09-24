import type { Meta, StoryObj } from '@storybook/react-vite';
import { useTranslation } from 'react-i18next';
import { Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { DataTable, DataTableToolbar, type FilterValues } from '@/shared/ui/data-table';
import {
  K8sListBody,
  K8sNoResultsState,
  K8sStatusStrip,
  countByStatusFacet,
  getK8sColumns,
  sumResourceTotals,
} from '@/domains/catalog';
import type { K8sCluster, K8sStatus } from '@/domains/catalog';

/**
 * /k8s list page stories.
 *
 * Renders `K8sListBody` (the whole page body incl. the status strip) with
 * deterministic fixture fleets so baselines never depend on mock state.
 * `StatusChipActive` and `NoResults` render the controlled strip + table
 * directly — the body owns its filter state internally, so pre-set filter
 * states are shown through the strip's controlled props instead.
 */

const GIB = 1024 ** 3;

const FLEET: K8sCluster[] = [
  {
    id: 'k8s-prod',
    name: 'prod-k3s',
    status: 'running',
    version: 'v1.31.1+k3s1',
    cpNodes: 3,
    workerNodes: 5,
    vcpu: 40,
    ramBytes: 96 * GIB,
    cni: 'Cilium',
    fleet: 'prod-cluster',
    endpoint: 'https://10.0.0.10:6443',
    createdAt: '2026-03-12T00:00:00Z',
  },
  {
    id: 'k8s-staging',
    name: 'staging-k3s',
    status: 'running',
    version: 'v1.30.5+k3s1',
    cpNodes: 1,
    workerNodes: 3,
    vcpu: 16,
    ramBytes: 32 * GIB,
    cni: 'Flannel',
    fleet: 'prod-cluster',
    endpoint: 'https://10.0.0.20:6443',
    createdAt: '2026-04-28T00:00:00Z',
  },
  {
    id: 'k8s-edge',
    name: 'edge-k3s',
    status: 'provisioning',
    version: 'v1.31.1+k3s1',
    cpNodes: 1,
    workerNodes: 2,
    vcpu: 8,
    ramBytes: 16 * GIB,
    cni: 'Flannel',
    fleet: 'edge-cluster',
    endpoint: 'https://10.1.0.10:6443',
    createdAt: '2026-07-08T09:15:00Z',
  },
  {
    id: 'k8s-dev',
    name: 'dev-k3s',
    status: 'error',
    version: 'v1.30.5+k3s1',
    cpNodes: 1,
    workerNodes: 1,
    vcpu: 4,
    ramBytes: 8 * GIB,
    cni: 'Flannel',
    fleet: 'edge-cluster',
    endpoint: 'https://10.1.0.20:6443',
    createdAt: '2026-08-30T11:00:00Z',
  },
];

const noop = () => {};

/** Shared page frame for the controlled-strip stories (mirrors K8sListBody's). */
function StripStoryFrame({ activeStatus, children }: { activeStatus: K8sStatus | null; children?: React.ReactNode }) {
  const { t } = useTranslation();
  const columns = getK8sColumns(t);
  const counts = countByStatusFacet(FLEET);
  const visible = activeStatus ? FLEET.filter((cluster) => cluster.status === activeStatus) : FLEET;
  const filters: FilterValues = activeStatus ? { name: '', status: activeStatus } : { name: '', status: '' };
  const running = counts.running;

  return (
    <PageTemplate
      data-od-id="k8s-list"
      title={t('k8s.list.title')}
      width="wide"
      description={
        <span>
          <MonoNum>{running}</MonoNum> <span className="text-muted-foreground">{t('k8s.list.runningOf')}</span>{' '}
          <MonoNum>{FLEET.length}</MonoNum> <span className="text-muted-foreground">{t('k8s.list.total')}</span>
        </span>
      }
      actions={
        <Button>
          <Add />
          {t('k8s.list.create')}
        </Button>
      }
    >
      <div className="space-y-2">
        <K8sStatusStrip
          counts={counts}
          activeStatus={activeStatus}
          onToggleStatus={noop}
          totals={sumResourceTotals(visible)}
        />
        <DataTableToolbar columns={columns} filters={filters} onFiltersChange={noop} />
        <DataTable columns={columns} data={visible} density="compact" onRowClick={noop} />
        {children}
      </div>
    </PageTemplate>
  );
}

/** Table filtered to the active chip — same composition the body produces. */
function StatusChipActiveBody() {
  return <StripStoryFrame activeStatus="provisioning" />;
}

/** Active chip on a status with zero matches — strip + no-results state. */
function NoResultsBody() {
  const { t } = useTranslation();
  return (
    <StripStoryFrame activeStatus="degraded">
      <K8sNoResultsState
        title={t('k8s.list.empty.noResultsStatusTitle', { status: t('k8s.status.degraded') })}
        onReset={noop}
      />
    </StripStoryFrame>
  );
}

const meta = {
  title: 'Pages/K8sList',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: fleet with mixed statuses, strip chips + toolbar + table. */
export const Default: Story = {
  render: () => <K8sListBody items={FLEET} onCreate={noop} />,
};

/** Loading: strip skeleton + table row skeletons. */
export const Loading: Story = {
  render: () => <K8sListBody items={[]} isPending onCreate={noop} />,
};

/** Empty fleet: no strip, empty state with a create CTA and docs links. */
export const Empty: Story = {
  render: () => <K8sListBody items={[]} onCreate={noop} />,
};

/** One chip active: provisioning chip emphasized, table filtered to it. */
export const StatusChipActive: Story = {
  render: () => <StatusChipActiveBody />,
};

/** No results: degraded chip active with zero degraded clusters — status-aware empty state. */
export const NoResults: Story = {
  render: () => <NoResultsBody />,
};
