import type { Meta, StoryObj } from '@storybook/react-vite';
import type { DbEngine } from '@/domains/catalog';
import { ManagedLanding } from '@/domains/catalog';
import { listEngines } from '@/shared/api/mocks/handmade/databases';

/**
 * /managed landing stories — the engine-catalog card grid. Fixtures come
 * from the catalog mock (deterministic module-level data); cluster counts
 * are inline so the baselines don't depend on the cluster fixtures.
 */

const noop = () => {};

const engines: DbEngine[] = listEngines();

/** Counts that exercise the range the card shows: zero, one, several. */
const COUNTS: Record<string, number> = {
  postgres: 0,
  redis: 2,
  garnet: 1,
  clickhouse: 2,
  kafka: 1,
};

const meta = {
  title: 'Pages/ManagedLanding',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: full engine catalog with mixed cluster counts. */
export const Default: Story = {
  render: () => <ManagedLanding engines={engines} clusterCounts={COUNTS} onOpenEngine={noop} />,
};

/** Loading: card-grid skeleton while the catalog resolves. */
export const Loading: Story = {
  render: () => <ManagedLanding engines={[]} clusterCounts={{}} isPending onOpenEngine={noop} />,
};

/** Empty catalog: defensive state (the shipped catalog always has engines). */
export const Empty: Story = {
  render: () => <ManagedLanding engines={[]} clusterCounts={{}} onOpenEngine={noop} />,
};
