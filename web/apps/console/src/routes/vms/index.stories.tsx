import type { Meta, StoryObj } from '@storybook/react-vite';
import { useTranslation } from 'react-i18next';
import { Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import type { Vm, VmStatus } from '@/shared/api';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { DataTable, DataTableToolbar, type FilterValues } from '@/shared/ui/data-table';
import {
  VmListBody,
  VmNoResultsState,
  VmStatusStrip,
  countByStatusFacet,
  getVmColumns,
  sumResourceTotals,
} from '@/features/vms';

/**
 * /vms list page stories.
 *
 * Renders `VmListBody` (the whole page body incl. the status strip) with
 * deterministic fixture fleets so baselines never depend on kubb/MSW state.
 * `StatusChipActive` and `NoResults` render the controlled strip + table
 * directly — the body owns its filter state internally, so pre-set filter
 * states are shown through the strip's controlled props instead.
 */

const FLEET: Vm[] = [
  { id: 'vm-a8c91f2e', name: 'web-prod-01', status: 'running', internalIp: '10.128.1.10', zone: 'eu-central-1', machineType: '4-8', vcpu: 4, ramGb: 8, diskGb: 80, createdAt: '2026-07-25T09:00:00.000Z' },
  { id: 'vm-b7d40e1a', name: 'api-prod-01', status: 'running', internalIp: '10.128.1.11', zone: 'eu-central-1', machineType: '4-8', vcpu: 4, ramGb: 8, diskGb: 60, createdAt: '2026-07-27T09:00:00.000Z' },
  { id: 'vm-c2f8a039', name: 'worker-01', status: 'running', internalIp: '10.128.2.20', zone: 'eu-central-1', machineType: '2-4', vcpu: 2, ramGb: 4, diskGb: 40, createdAt: '2026-08-04T09:00:00.000Z' },
  { id: 'vm-9e1b3c47', name: 'db-replica-01', status: 'running', internalIp: '10.128.3.5', zone: 'eu-central-1', machineType: '8-32', vcpu: 8, ramGb: 32, diskGb: 500, createdAt: '2026-08-09T09:00:00.000Z' },
  { id: 'vm-6f8d22b8', name: 'build-runner', status: 'error', internalIp: '10.128.5.3', zone: 'eu-central-1', machineType: '4-8', vcpu: 4, ramGb: 8, diskGb: 100, createdAt: '2026-08-19T09:00:00.000Z' },
  { id: 'vm-1b9e4f73', name: 'staging-api', status: 'stopped', internalIp: '10.128.6.12', zone: 'eu-central-1', machineType: '2-4', vcpu: 2, ramGb: 4, diskGb: 40, createdAt: '2026-08-24T09:00:00.000Z' },
  { id: 'vm-4d2a89e1', name: 'ml-trainer', status: 'provisioning', internalIp: '10.128.7.4', zone: 'eu-central-1', machineType: '8-64', vcpu: 8, ramGb: 64, diskGb: 250, createdAt: '2026-09-23T09:00:00.000Z' },
];

const noop = () => {};

/** Shared page frame for the controlled-strip stories (mirrors VmListBody's). */
function StripStoryFrame({ activeStatus, children }: { activeStatus: VmStatus | null; children?: React.ReactNode }) {
  const { t } = useTranslation();
  const columns = getVmColumns(t);
  const counts = countByStatusFacet(FLEET);
  const visible = activeStatus ? FLEET.filter((vm) => vm.status === activeStatus) : FLEET;
  const filters: FilterValues = activeStatus ? { q: '', status: activeStatus, zone: '' } : { q: '', status: '', zone: '' };
  const running = counts.running;

  return (
    <PageTemplate
      data-od-id="vms-list"
      title={t('vms.list.title')}
      width="wide"
      description={
        <span>
          <MonoNum>{running}</MonoNum> <span className="text-muted-foreground">{t('vms.list.runningOf')}</span>{' '}
          <MonoNum>{FLEET.length}</MonoNum> <span className="text-muted-foreground">{t('vms.list.total')}</span>
        </span>
      }
      actions={
        <Button>
          <Add />
          {t('vms.list.create')}
        </Button>
      }
    >
      <div className="space-y-2">
        <VmStatusStrip
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
  return <StripStoryFrame activeStatus="stopped" />;
}

/** Active chip on a status with zero matches — strip + no-results state. */
function NoResultsBody() {
  const { t } = useTranslation();
  return (
    <StripStoryFrame activeStatus="idle">
      <VmNoResultsState title={t('vms.list.empty.noResultsStatusTitle', { status: t('vms.status.idle') })} onReset={noop} />
    </StripStoryFrame>
  );
}

const meta = {
  title: 'Pages/VmList',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: fleet with mixed statuses, strip chips + toolbar + table. */
export const Default: Story = {
  render: () => (
    <VmListBody
      items={FLEET}
      isPending={false}
      isError={false}
      error={null}
      onRetry={noop}
      onCreate={noop}
      onOpenVm={noop}
    />
  ),
};

/** Loading: strip skeleton + table row skeletons. */
export const Loading: Story = {
  render: () => (
    <VmListBody
      items={[]}
      isPending
      isError={false}
      error={null}
      onRetry={noop}
      onCreate={noop}
      onOpenVm={noop}
    />
  ),
};

/** Empty fleet: no strip, empty state with a create CTA. */
export const Empty: Story = {
  render: () => (
    <VmListBody
      items={[]}
      isPending={false}
      isError={false}
      error={null}
      onRetry={noop}
      onCreate={noop}
      onOpenVm={noop}
    />
  ),
};

/** One chip active: stopped chip emphasized, table filtered to stopped VMs. */
export const StatusChipActive: Story = {
  render: () => <StatusChipActiveBody />,
};

/** No results: idle chip active with zero idle VMs — status-aware empty state. */
export const NoResults: Story = {
  render: () => <NoResultsBody />,
};
