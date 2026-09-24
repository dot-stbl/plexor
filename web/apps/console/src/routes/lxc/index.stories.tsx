import type { Meta, StoryObj } from '@storybook/react-vite';
import { useTranslation } from 'react-i18next';
import { Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { DataTable, DataTableToolbar, type FilterValues } from '@/shared/ui/data-table';
import {
  LxcListBody,
  LxcNoResultsState,
  LxcStatusStrip,
  countLxcByStatusFacet,
  getLxcColumns,
  sumLxcResourceTotals,
} from '@/domains/compute';
import type { LxcContainer, LxcStatus } from '@/domains/compute';

/**
 * /lxc list page stories.
 *
 * Renders `LxcListBody` (the whole page body incl. the status strip) with
 * deterministic fixture inventories so baselines never depend on mock state.
 * `StatusChipActive` and `NoResults` render the controlled strip + table
 * directly — the body owns its filter state internally, so pre-set filter
 * states are shown through the strip's controlled props instead.
 */

const GIB = 1024 ** 3;

const INVENTORY: LxcContainer[] = [
  {
    id: 'lxc-web-01',
    name: 'web-01',
    status: 'running',
    template: 'ubuntu-24.04',
    os: 'Ubuntu',
    osVersion: '24.04',
    cores: 2,
    ramBytes: 2 * GIB,
    rootfsBytes: 12 * GIB,
    unprivileged: true,
    nodeHostname: 'node-a.local',
    ip: '10.0.0.31',
    createdAt: '2026-03-12T09:20:00Z',
  },
  {
    id: 'lxc-web-02',
    name: 'web-02',
    status: 'running',
    template: 'ubuntu-24.04',
    os: 'Ubuntu',
    osVersion: '24.04',
    cores: 2,
    ramBytes: 2 * GIB,
    rootfsBytes: 12 * GIB,
    unprivileged: true,
    nodeHostname: 'node-b.local',
    ip: '10.0.0.32',
    createdAt: '2026-03-12T09:22:00Z',
  },
  {
    id: 'lxc-cache-01',
    name: 'cache-01',
    status: 'running',
    template: 'alpine-3.20',
    os: 'Alpine',
    osVersion: '3.20',
    cores: 1,
    ramBytes: Math.round(0.5 * GIB),
    rootfsBytes: 4 * GIB,
    unprivileged: true,
    nodeHostname: 'node-a.local',
    ip: '10.0.0.33',
    createdAt: '2026-04-02T14:05:00Z',
  },
  {
    id: 'lxc-build-01',
    name: 'build-01',
    status: 'stopped',
    template: 'debian-12',
    os: 'Debian',
    osVersion: '12',
    cores: 4,
    ramBytes: 4 * GIB,
    rootfsBytes: 20 * GIB,
    unprivileged: false,
    nodeHostname: 'node-b.local',
    ip: '10.0.0.34',
    createdAt: '2026-05-18T11:40:00Z',
  },
  {
    id: 'lxc-metrics-01',
    name: 'metrics-01',
    status: 'error',
    template: 'ubuntu-24.04',
    os: 'Ubuntu',
    osVersion: '24.04',
    cores: 2,
    ramBytes: 1 * GIB,
    rootfsBytes: 10 * GIB,
    unprivileged: true,
    nodeHostname: 'node-b.local',
    ip: '10.0.0.36',
    createdAt: '2026-06-30T17:50:00Z',
  },
];

const noop = () => {};

/** Shared page frame for the controlled-strip stories (mirrors LxcListBody's). */
function StripStoryFrame({ activeStatus, children }: { activeStatus: LxcStatus | null; children?: React.ReactNode }) {
  const { t } = useTranslation();
  const columns = getLxcColumns(t);
  const counts = countLxcByStatusFacet(INVENTORY);
  const visible = activeStatus ? INVENTORY.filter((container) => container.status === activeStatus) : INVENTORY;
  const filters: FilterValues = activeStatus ? { name: '', status: activeStatus } : { name: '', status: '' };
  const running = counts.running;

  return (
    <PageTemplate
      data-od-id="lxc-list"
      title={t('lxc.list.title')}
      width="wide"
      description={
        <span>
          <MonoNum>{running}</MonoNum> <span className="text-muted-foreground">{t('lxc.list.runningOf')}</span>{' '}
          <MonoNum>{INVENTORY.length}</MonoNum> <span className="text-muted-foreground">{t('lxc.list.total')}</span>
        </span>
      }
      actions={
        <Button>
          <Add />
          {t('lxc.list.create')}
        </Button>
      }
    >
      <div className="space-y-2">
        <LxcStatusStrip
          counts={counts}
          activeStatus={activeStatus}
          onToggleStatus={noop}
          totals={sumLxcResourceTotals(visible)}
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
  return <StripStoryFrame activeStatus="stopped" />;
}

/** Active chip on a status with zero matches — strip + no-results state. */
function NoResultsBody() {
  const { t } = useTranslation();
  return (
    <StripStoryFrame activeStatus="paused">
      <LxcNoResultsState
        title={t('lxc.list.empty.noResultsStatusTitle', { status: t('lxc.status.paused') })}
        onReset={noop}
      />
    </StripStoryFrame>
  );
}

const meta = {
  title: 'Pages/LxcList',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: inventory with mixed statuses, strip chips + toolbar + table. */
export const Default: Story = {
  render: () => <LxcListBody items={INVENTORY} onCreate={noop} />,
};

/** Loading: strip skeleton + table row skeletons. */
export const Loading: Story = {
  render: () => <LxcListBody items={[]} isPending onCreate={noop} />,
};

/** Empty inventory: no strip, empty state with a create CTA and docs links. */
export const Empty: Story = {
  render: () => <LxcListBody items={[]} onCreate={noop} />,
};

/** One chip active: stopped chip emphasized, table filtered to stopped containers. */
export const StatusChipActive: Story = {
  render: () => <StatusChipActiveBody />,
};

/** No results: paused chip active with zero paused containers — status-aware empty state. */
export const NoResults: Story = {
  render: () => <NoResultsBody />,
};
