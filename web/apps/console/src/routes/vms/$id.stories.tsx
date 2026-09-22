import type { Meta, StoryObj } from '@storybook/react-vite';
import { useTranslation } from 'react-i18next';
import {
  ArrowBack,
  Delete,
  PlayArrow,
  Stop
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import type { VmDetail } from '@/shared/api';
import { Button } from '@/shared/ui/primitives/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle
} from '@/shared/ui/primitives/card';
import { PageTemplate } from '@/shared/ui/app-shell';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { CopyableText } from '@/shared/ui/primitives/copyable-text';
import { CopyButton } from '@/shared/ui/primitives/copy-button';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/shared/ui/primitives/alert';
import { mapVmStatusToVariant } from '@/features/vms';

/**
 * /vms/$id page stories.
 *
 * Renders the populated body directly with deterministic fixture data so
 * baselines never depend on kubb/MSW state. Mirrors the route's layout but
 * skips the lifecycle-form wiring (mutations, AlertDialog state, document
 * title, navigation). If the route's body changes, update these fixtures.
 */

const RUNNING_VM: VmDetail = {
  id: 'a1b2c3d4-1111-4111-8111-aaaa1111aaaa',
  name: 'web-prod-01',
  status: 'running',
  internalIp: '10.128.4.21',
  zone: 'eu-central-1a',
  machineType: 'standard-4',
  vcpu: 4,
  ramGb: 16,
  diskGb: 120,
  tags: ['prod', 'web'],
  createdAt: '2026-09-22T13:42:08.114Z',
  description: 'Front-end nodes',
  label: 'web',
  project: 'plexor-core',
  vpcId: 'b2c3d4e5-2222-4222-8222-bbbb2222bbbb',
  subnetId: 'c3d4e5f6-3333-4333-8333-cccc3333cccc',
  securityGroups: ['default', 'web-ingress'],
  image: 'Ubuntu 24.04 LTS',
  diskEncrypted: true,
  publicIp: '203.0.113.42',
  updatedAt: '2026-09-22T14:00:00.000Z',
};

const STOPPED_VM: VmDetail = {
  ...RUNNING_VM,
  id: 'd4e5f6a7-4444-4444-8444-dddd4444dddd',
  name: 'web-staging-02',
  status: 'stopped',
  tags: ['staging'],
  publicIp: null,
  updatedAt: '2026-09-22T15:00:00.000Z',
};

const ERROR_VM: VmDetail = {
  ...RUNNING_VM,
  id: 'e5f6a7b8-5555-4555-8555-eeee5555eeee',
  name: 'batch-runner-03',
  status: 'error',
  tags: ['batch'],
  publicIp: null,
  description: null,
  label: null,
  updatedAt: '2026-09-22T16:00:00.000Z',
};

interface DetailRowProps {
  label: string;
  children: React.ReactNode;
}

function DetailRow({ label, children }: DetailRowProps) {
  return (
    <div className="flex items-start justify-between gap-3 border-b border-border/50 py-1.5 last:border-b-0">
      <span className="shrink-0 text-xs text-muted-foreground">{label}</span>
      <span className="min-w-0 text-right text-xs">{children}</span>
    </div>
  );
}

/** Right-aligned cell that places a value and an inline Copy affordance flush
 *  against each other. Single-line when both fit; the value truncates. */
function InlineValue({ children }: { children: React.ReactNode }) {
  return <span className="inline-flex items-center justify-end gap-1.5">{children}</span>;
}

function VmDetailBody({ vm }: { vm: VmDetail }) {
  const { t } = useTranslation();
  const isProvisioning = vm.status === 'provisioning';
  const isErrorState = vm.status === 'error';
  const canStart = vm.status === 'stopped' || vm.status === 'idle' || isErrorState;
  const canStop = vm.status === 'running' || vm.status === 'idle';

  return (
    <PageTemplate
      title={vm.name}
      width="wide"
      data-od-id="vm-detail"
      description={
        <span className="inline-flex items-center gap-2">
          <StatusPill variant={mapVmStatusToVariant(vm.status)} size="sm">
            {vm.status}
          </StatusPill>
          <span className="text-muted-foreground">·</span>
          <span className="text-muted-foreground">{t('vms.detail.subtitle')}</span>
        </span>
      }
      actions={
        <>
          <Button variant="ghost">
            <ArrowBack />
            {t('vms.detail.back')}
          </Button>
          <Button variant="outline" disabled={!canStart}>
            <PlayArrow />
            {t('vms.detail.lifecycle.start')}
          </Button>
          <Button variant="outline" disabled={!canStop}>
            <Stop />
            {t('vms.detail.lifecycle.stop')}
          </Button>
          <Button variant="destructive">
            <Delete />
            {t('vms.detail.lifecycle.delete')}
          </Button>
        </>
      }
    >
      <div className="grid gap-4 md:grid-cols-2">
        <Card className="gap-0 p-0">
          <CardHeader className="gap-0.5 border-b border-border p-4">
            <CardTitle className="text-sm">{t('vms.detail.section.identity')}</CardTitle>
            <CardDescription>{vm.id}</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            <DetailRow label={t('vms.detail.field.name')}>
              <InlineValue>
                {vm.name}
                <CopyButton value={vm.name} copyLabel={t('table.copy.name')} />
              </InlineValue>
            </DetailRow>
            <DetailRow label={t('vms.detail.field.id')}>
              <CopyableText value={vm.id} copyLabel={t('table.copy.id')}>
                <MonoNum muted>{vm.id}</MonoNum>
              </CopyableText>
            </DetailRow>
            <DetailRow label={t('vms.detail.field.status')}>
              <StatusPill variant={mapVmStatusToVariant(vm.status)} size="sm">
                {vm.status}
              </StatusPill>
            </DetailRow>
            <DetailRow label={t('vms.detail.field.zone')}>
              <CopyableText value={vm.zone} copyLabel={t('table.copy.zone')}>
                {vm.zone}
              </CopyableText>
            </DetailRow>
            <DetailRow label={t('vms.detail.field.image')}>
              <InlineValue>
                {vm.image}
                <CopyButton value={vm.image} copyLabel={t('table.copy.image')} />
              </InlineValue>
            </DetailRow>
            <DetailRow label={t('vms.detail.field.machineType')}>
              <InlineValue>
                {vm.machineType}
                <CopyButton value={vm.machineType} copyLabel={t('table.copy.machineType')} />
              </InlineValue>
            </DetailRow>
            <DetailRow label={t('vms.detail.field.vcpu')}>
              <MonoNum>{vm.vcpu}</MonoNum>
            </DetailRow>
            <DetailRow label={t('vms.detail.field.ram')}>
              <MonoNum>{vm.ramGb}</MonoNum> GB
            </DetailRow>
            <DetailRow label={t('vms.detail.field.disk')}>
              <MonoNum>{vm.diskGb}</MonoNum> GB
            </DetailRow>
            <DetailRow label={t('vms.detail.field.diskEncrypted')}>
              {vm.diskEncrypted ? t('common.yes') : t('common.no')}
            </DetailRow>
            <DetailRow label={t('vms.detail.field.createdAt')}>
              {new Date(vm.createdAt).toLocaleString()}
            </DetailRow>
            {vm.updatedAt ? (
              <DetailRow label={t('vms.detail.field.updatedAt')}>
                {new Date(vm.updatedAt).toLocaleString()}
              </DetailRow>
            ) : null}
            {vm.tags && vm.tags.length > 0 ? (
              <DetailRow label={t('vms.detail.field.tags')}>
                <span className="flex flex-wrap gap-1">
                  {vm.tags.map((tag) => (
                    <span
                      key={tag}
                      className="rounded border border-border bg-background px-1.5 py-0.5 font-mono text-[10px] uppercase"
                    >
                      {tag}
                    </span>
                  ))}
                </span>
              </DetailRow>
            ) : (
              <DetailRow label={t('vms.detail.field.tags')}>
                <span className="text-muted-foreground">{t('vms.detail.field.tagsEmpty')}</span>
              </DetailRow>
            )}
          </CardContent>
        </Card>

        <div className="flex flex-col gap-4">
          <Card className="gap-0 p-0">
            <CardHeader className="gap-0.5 border-b border-border p-4">
              <CardTitle className="text-sm">{t('vms.detail.section.placement')}</CardTitle>
              <CardDescription>{vm.project}</CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-2">
              <DetailRow label={t('vms.detail.field.project')}>
              <InlineValue>
                {vm.project}
                <CopyButton value={vm.project} copyLabel={t('table.copy.name')} />
              </InlineValue>
            </DetailRow>
              <DetailRow label={t('vms.detail.field.vpc')}>
                <CopyableText value={vm.vpcId} copyLabel={t('table.copy.id')}>
                  <MonoNum muted>{vm.vpcId}</MonoNum>
                </CopyableText>
              </DetailRow>
              <DetailRow label={t('vms.detail.field.subnet')}>
                <CopyableText value={vm.subnetId} copyLabel={t('table.copy.id')}>
                  <MonoNum muted>{vm.subnetId}</MonoNum>
                </CopyableText>
              </DetailRow>
            </CardContent>
          </Card>

          <Card className="gap-0 p-0">
            <CardHeader className="gap-0.5 border-b border-border p-4">
              <CardTitle className="text-sm">{t('vms.detail.section.network')}</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-2">
              <DetailRow label={t('vms.detail.field.internalIp')}>
                <CopyableText value={vm.internalIp} copyLabel={t('table.copy.ip')}>
                  <MonoNum muted>{vm.internalIp}</MonoNum>
                </CopyableText>
              </DetailRow>
              {vm.publicIp ? (
                <DetailRow label={t('vms.detail.field.publicIp')}>
                  <CopyableText value={vm.publicIp} copyLabel={t('table.copy.ip')}>
                    <MonoNum muted>{vm.publicIp}</MonoNum>
                  </CopyableText>
                </DetailRow>
              ) : null}
              {vm.securityGroups && vm.securityGroups.length > 0 ? (
                <DetailRow label={t('vms.detail.field.securityGroups')}>
                  <span className="flex flex-wrap gap-1">
                    {vm.securityGroups.map((sg) => (
                      <span
                        key={sg}
                        className="rounded border border-border bg-background px-1.5 py-0.5 font-mono text-[10px]"
                      >
                        {sg}
                      </span>
                    ))}
                  </span>
                </DetailRow>
              ) : null}
            </CardContent>
          </Card>

          <Card className="gap-0 p-0">
            <CardHeader className="gap-0.5 border-b border-border p-4">
              <CardTitle className="text-sm">{t('vms.detail.section.lifecycle')}</CardTitle>
              <CardDescription>
                {isProvisioning
                  ? t('vms.detail.lifecycle.provisioningHint')
                  : isErrorState
                    ? t('vms.detail.lifecycle.errorHint')
                    : null}
              </CardDescription>
            </CardHeader>
            <CardContent className="flex flex-wrap items-center gap-2">
              <Button size="sm" disabled={!canStart}>
                <PlayArrow />
                {t('vms.detail.lifecycle.start')}
              </Button>
              <Button variant="outline" size="sm" disabled={!canStop}>
                <Stop />
                {t('vms.detail.lifecycle.stop')}
              </Button>
              <Button variant="destructive" size="sm">
                <Delete />
                {t('vms.detail.lifecycle.delete')}
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>
    </PageTemplate>
  );
}

function VmDetailSkeletonBody() {
  const { t } = useTranslation();
  return (
    <PageTemplate
      title={t('vms.detail.title')}
      width="wide"
      data-od-id="vm-detail-skeleton"
      actions={
        <Button variant="ghost">
          <ArrowBack />
          {t('vms.detail.back')}
        </Button>
      }
    >
      <div className="flex flex-col gap-2">
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-20 w-full" />
      </div>
    </PageTemplate>
  );
}

function VmDetailNotFoundBody({ id }: { id: string }) {
  const { t } = useTranslation();
  return (
    <PageTemplate
      title={t('vms.detail.notFound')}
      width="default"
      data-od-id="vm-detail-not-found"
      actions={
        <Button variant="ghost">
          <ArrowBack />
          {t('vms.detail.back')}
        </Button>
      }
    >
      <Alert variant="destructive">
        <AlertTitle>{t('vms.detail.notFound')}</AlertTitle>
        <AlertDescription>{t('vms.detail.notFoundDescription', { id })}</AlertDescription>
      </Alert>
    </PageTemplate>
  );
}

const meta = {
  title: 'Pages/VmDetail',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: a running VM with full identity, placement, network, lifecycle. */
export const Running: Story = {
  render: () => <VmDetailBody vm={RUNNING_VM} />,
};

/** Stopped VM: lifecycle card disables Stop (only Start is actionable). */
export const Stopped: Story = {
  render: () => <VmDetailBody vm={STOPPED_VM} />,
};

/** Error VM: both Start and Stop are enabled with the error hint visible. */
export const Error: Story = {
  render: () => <VmDetailBody vm={ERROR_VM} />,
};

/** Loading: skeleton placeholders for the three body sections. */
export const Loading: Story = {
  render: () => <VmDetailSkeletonBody />,
};

/** Not found: 404 alert when the VM doesn't exist. */
export const NotFound: Story = {
  render: () => <VmDetailNotFoundBody id="00000000-0000-0000-0000-000000000000" />,
};
