import type { Meta, StoryObj } from '@storybook/react-vite';
import { useTranslation } from 'react-i18next';
import { Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import type { DbCluster, DbStatus } from '@/domains/catalog';
import { getEngine } from '@/shared/api/mocks/handmade/databases';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { DataTable, DataTableColumns, type DataTableColumnsState } from '@/shared/ui/data-table';
import {
  ManagedServiceListBody,
  DbStatusStrip,
  countDbByStatusFacet,
  dbStatusLabelKey,
  getDbColumns,
  sumDbTotals,
} from '@/domains/catalog';

/**
 * /managed/<engine> page stories — PostgreSQL as the representative engine
 * (one set for the shared `ManagedServicePage`; the other four engines render
 * the same components with different catalog metadata).
 *
 * `ManagedServiceListBody` renders with a deterministic fixture fleet so the
 * baselines never depend on the handmade mock module (which keeps PostgreSQL
 * empty on purpose to demo the onboarding empty state — that state is covered
 * by `bun run shot page /managed/postgres` against the real router).
 * `StatusChipActive` and `NoResults` render the controlled strip + table
 * directly — the body owns its filter state internally.
 */

/** Resolve an engine or fail fast — the catalog mock always has all five. */
function requireEngine(id: string) {
  const engine = getEngine(id);
  if (!engine) {
    throw new Error(`engine "${id}" missing from the catalog mock`);
  }
  return engine;
}

const postgres = requireEngine('postgres');

const FLEET: DbCluster[] = [
  {
    id: 'db-pg-oltp',
    name: 'pg-oltp',
    engineId: 'postgres',
    kind: 'relational',
    runtime: 'vm',
    nodeId: 'node-a',
    hostname: 'node-a.local',
    dns: 'pg-oltp.db.plexor.internal',
    status: 'running',
    version: '16.4',
    storageGb: 120,
    backupsEnabled: true,
    bindings: 5,
    createdAt: '2026-06-10T09:00:00Z',
  },
  {
    id: 'db-pg-reporting',
    name: 'pg-reporting',
    engineId: 'postgres',
    kind: 'relational',
    runtime: 'lxc',
    nodeId: 'node-b',
    hostname: 'node-b.local',
    dns: 'pg-reporting.db.plexor.internal',
    status: 'running',
    version: '16.4',
    storageGb: 300,
    backupsEnabled: true,
    bindings: 2,
    createdAt: '2026-06-18T14:30:00Z',
  },
  {
    id: 'db-pg-staging',
    name: 'pg-staging',
    engineId: 'postgres',
    kind: 'relational',
    runtime: 'docker',
    nodeId: 'node-a',
    hostname: 'node-a.local',
    dns: 'pg-staging.db.plexor.internal',
    status: 'degraded',
    version: '16.4',
    storageGb: 40,
    backupsEnabled: false,
    bindings: 1,
    createdAt: '2026-07-22T10:15:00Z',
  },
  {
    id: 'db-pg-analytics',
    name: 'pg-analytics',
    engineId: 'postgres',
    kind: 'relational',
    runtime: 'k8s',
    nodeId: 'node-c',
    hostname: 'node-c.local',
    dns: 'pg-analytics.db.plexor.internal',
    status: 'deploying',
    version: '16.4',
    storageGb: 200,
    backupsEnabled: false,
    bindings: 0,
    createdAt: '2026-09-21T08:45:00Z',
  },
  {
    id: 'db-pg-legacy',
    name: 'pg-legacy',
    engineId: 'postgres',
    kind: 'relational',
    runtime: 'vm',
    nodeId: 'node-b',
    hostname: 'node-b.local',
    dns: 'pg-legacy.db.plexor.internal',
    status: 'stopped',
    version: '14.9',
    storageGb: 60,
    backupsEnabled: false,
    bindings: 0,
    createdAt: '2025-11-02T12:00:00Z',
  },
];

const noop = () => {};

const COL_STATE: DataTableColumnsState = { hidden: [], order: [] };

/** Shared page frame for the controlled-strip stories (mirrors the body's). */
function StripStoryFrame({ activeStatus, children }: { activeStatus: DbStatus | null; children?: React.ReactNode }) {
  const { t } = useTranslation();
  const columns = getDbColumns(t);
  const counts = countDbByStatusFacet(FLEET);
  const visible = activeStatus ? FLEET.filter((cluster) => cluster.status === activeStatus) : FLEET;
  const running = counts.running;

  return (
    <PageTemplate
      data-od-id={`managed-${postgres.id}`}
      title={postgres.name}
      width="wide"
      description={
        <span>
          <MonoNum>{running}</MonoNum> <span className="text-muted-foreground">{t('managed.list.runningOf')}</span>{' '}
          <MonoNum>{FLEET.length}</MonoNum> <span className="text-muted-foreground">{t('managed.list.total')}</span>
        </span>
      }
      actions={
        <Button>
          <Add className="size-3.5" />
          {t('managed.list.create')}
        </Button>
      }
    >
      <div className="space-y-2">
        <DbStatusStrip
          counts={counts}
          activeStatus={activeStatus}
          onToggleStatus={noop}
          totals={sumDbTotals(visible)}
        />
        <div className="flex justify-end">
          <DataTableColumns columns={columns} value={COL_STATE} onChange={noop} />
        </div>
        <DataTable columns={columns} data={visible} density="compact" />
        {children}
      </div>
    </PageTemplate>
  );
}

/** Table filtered to the active chip — same composition the body produces. */
function StatusChipActiveBody() {
  return <StripStoryFrame activeStatus="degraded" />;
}

/** Active chip on a status with zero matches — strip + no-results state. */
function NoResultsBody() {
  const { t } = useTranslation();
  return (
    <StripStoryFrame activeStatus="error">
      <p className="py-6 text-center text-sm text-muted-foreground">
        {t('managed.list.empty.noResultsStatusTitle', { status: t(dbStatusLabelKey('error')) })}
      </p>
    </StripStoryFrame>
  );
}

const meta = {
  title: 'Pages/ManagedPostgres',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: engine fleet with mixed statuses, strip chips + column manager + table. */
export const Default: Story = {
  render: () => <ManagedServiceListBody engine={postgres} clusters={FLEET} onCreate={noop} onOpenCluster={noop} />,
};

/** Loading: strip + table skeletons while the fleet resolves. */
export const Loading: Story = {
  render: () => (
    <ManagedServiceListBody engine={postgres} clusters={[]} isPending onCreate={noop} onOpenCluster={noop} />
  ),
};

/** One chip active: degraded chip emphasized, table filtered to degraded clusters. */
export const StatusChipActive: Story = {
  render: () => <StatusChipActiveBody />,
};

/** No results: error chip active with zero error clusters — status-aware empty state. */
export const NoResults: Story = {
  render: () => <NoResultsBody />,
};
