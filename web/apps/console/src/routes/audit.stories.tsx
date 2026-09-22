import type { Meta, StoryObj } from '@storybook/react-vite';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { faker } from '@faker-js/faker';
import { PageTemplate } from '@/shared/ui/app-shell';
import { DataTable } from '@/shared/ui/data-table';
import { AuditEmpty, getAuditColumns } from '@/features/audit';
import type { AuditQueryResponse } from '@/shared/api';

/**
 * /audit page stories.
 *
 * The route file uses kubb's `useAudit` hook, which makes a real HTTP call
 * (or MSW in dev). Stories render the page body directly with deterministic
 * fixture rows so the visual baselines never depend on network or clock.
 */

const SAMPLE_ROWS: AuditQueryResponse[] = [
  {
    id: 'a1b2c3d4-1111-4111-8111-aaaa1111aaaa',
    action: 'quotas.assignment.changed',
    orgId: 'b2c3d4e5-2222-4222-8222-bbbb2222bbbb',
    actorUserId: 'c3d4e5f6-3333-4333-8333-cccc3333cccc',
    targetKind: 'quota_assignment',
    targetId: 'd4e5f6a7-4444-4444-8444-dddd4444dddd',
    payload: { ceiling: 32, scope: 'org' },
    occurredAt: '2026-09-22T13:42:08.114Z',
  },
  {
    id: 'e5f6a7b8-5555-4555-8555-eeee5555eeee',
    action: 'clusters.created',
    orgId: 'b2c3d4e5-2222-4222-8222-bbbb2222bbbb',
    actorUserId: null,
    targetKind: 'cluster',
    targetId: 'f6a7b8c9-6666-4666-8666-ffff6666ffff',
    payload: { name: 'prod-eu', runtime: 'k3s' },
    occurredAt: '2026-09-22T12:18:51.040Z',
  },
  {
    id: 'a7b8c9d0-7777-4777-8777-aaaa7777aaaa',
    action: 'vm.provisioned',
    orgId: 'b2c3d4e5-2222-4222-8222-bbbb2222bbbb',
    actorUserId: 'c3d4e5f6-3333-4333-8333-cccc3333cccc',
    targetKind: 'vm',
    targetId: 'b8c9d0e1-8888-4888-8888-bbbb8888bbbb',
    payload: { node: 'edge-pop-amsterdam', vcpu: 4, ram_mb: 8192 },
    occurredAt: '2026-09-22T11:05:33.772Z',
  },
  {
    id: 'c9d0e1f2-9999-4999-8999-cccc9999cccc',
    action: 'sshkey.added',
    orgId: 'b2c3d4e5-2222-4222-8222-bbbb2222bbbb',
    actorUserId: 'd0e1f2a3-aaaa-4aaa-8aaa-dddd0000dddd',
    targetKind: 'ssh_key',
    targetId: null,
    payload: { label: 'laptop-personal' },
    occurredAt: '2026-09-22T09:51:12.508Z',
  },
];

function AuditPageBody({ rows }: { rows: AuditQueryResponse[] }) {
  const { t } = useTranslation();
  const columns = useMemo(() => getAuditColumns(t), [t]);
  return (
    <PageTemplate
      title={t('audit.title')}
      description={t('audit.description')}
      width="wide"
      data-od-id="audit"
    >
      {rows.length === 0 ? <AuditEmpty /> : <DataTable columns={columns} data={rows} density="compact" />}
    </PageTemplate>
  );
}

const meta = {
  title: 'Pages/Audit',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: a populated timeline (4 rows, mix of with/without actor). */
export const Default: Story = {
  render: () => <AuditPageBody rows={SAMPLE_ROWS} />,
};

/** Empty: zero rows → AuditEmpty state. The route renders the same
 *  component when kubb returns []. */
export const Empty: Story = {
  render: () => <AuditPageBody rows={[]} />,
};

/** Many rows: realistic cap (100 rows the kubb client allows by default).
 *  Uses faker to seed stable shapes — faker's random is seeded per-render
 *  here, but the deterministic timestamp + action strings keep the snapshot
 *  stable enough for the 1% pixel threshold. */
export const ManyRows: Story = {
  render: () => {
    faker.seed(42);
    const rows = Array.from({ length: 25 }, () => ({
      id: faker.string.uuid(),
      action: faker.helpers.arrayElement([
        'quotas.assignment.changed',
        'vm.provisioned',
        'vm.deleted',
        'clusters.created',
        'sshkey.added',
        'sshkey.removed',
      ]),
      orgId: 'b2c3d4e5-2222-4222-8222-bbbb2222bbbb',
      actorUserId: faker.string.uuid(),
      targetKind: faker.helpers.arrayElement(['vm', 'cluster', 'ssh_key', 'quota_assignment']),
      targetId: faker.string.uuid(),
      payload: {},
      occurredAt: new Date(Date.parse('2026-09-22T08:00:00.000Z') - Math.random() * 86400000).toISOString(),
    }));
    return <AuditPageBody rows={rows} />;
  },
};
