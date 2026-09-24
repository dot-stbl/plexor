import { useTranslation } from 'react-i18next';
import { ArrowBack } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Badge } from '@/shared/ui/primitives/badge';
import { Button } from '@/shared/ui/primitives/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/shared/ui/primitives/card';
import { CopyButton } from '@/shared/ui/primitives/copy-button';
import { CopyableText } from '@/shared/ui/primitives/copyable-text';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Size } from '@/shared/ui/primitives/size';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { TechIcon } from '@/shared/ui/primitives/tech-icon';
import type { K8sCluster } from '../model/k8s-types';
import { mapK8sStatusToVariant } from '../model/k8s-types';
import { k8sStatusLabelKey } from './k8s-status-strip';

interface K8sDetailBodyProps {
  /** The cluster to render. */
  cluster: K8sCluster;
  /** Navigate back to /k8s. */
  onBack: () => void;
}

/**
 * /k8s/$id page body: identity + capacity cards over one managed K3s
 * cluster. Fleet totals (vCPU / RAM) are the cluster's summed fields;
 * `endpoint` is the API server URL operators paste into a kubeconfig, so
 * it carries the copy affordance.
 *
 * Honest header: only Back. No Start/Stop/Delete — the handmade mock is
 * read-only and the contract has no lifecycle endpoints, so any mutation
 * button would be a fake. No "Open dashboard" either — the data carries
 * no dashboard URL.
 */
export function K8sDetailBody({ cluster, onBack }: K8sDetailBodyProps) {
  const { t } = useTranslation();

  return (
    <PageTemplate
      data-od-id="k8s-detail"
      title={cluster.name}
      width="wide"
      description={
        <span className="inline-flex items-center gap-2">
          <StatusPill variant={mapK8sStatusToVariant(cluster.status)} size="sm">
            {t(k8sStatusLabelKey(cluster.status))}
          </StatusPill>
          <span className="inline-block h-3 w-px bg-border" aria-hidden />
          <span className="text-muted-foreground">{t('k8s.detail.subtitle')}</span>
        </span>
      }
      actions={
        <Button variant="ghost" onClick={onBack}>
          <ArrowBack />
          {t('k8s.detail.back')}
        </Button>
      }
    >
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader className="border-b border-border">
            <CardTitle className="text-sm">{t('k8s.detail.section.identity')}</CardTitle>
            <CardDescription>{cluster.id}</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            <DetailRow label={t('k8s.detail.field.name')}>
              <InlineValue>
                {cluster.name}
                <CopyButton value={cluster.name} copyLabel={t('table.copy.name')} />
              </InlineValue>
            </DetailRow>
            <DetailRow label={t('k8s.detail.field.id')}>
              <CopyableText value={cluster.id} copyLabel={t('table.copy.id')}>
                <MonoNum muted>{cluster.id}</MonoNum>
              </CopyableText>
            </DetailRow>
            <DetailRow label={t('k8s.detail.field.status')}>
              <StatusPill variant={mapK8sStatusToVariant(cluster.status)} size="sm">
                {t(k8sStatusLabelKey(cluster.status))}
              </StatusPill>
            </DetailRow>
            <DetailRow label={t('k8s.detail.field.version')}>
              <InlineValue>
                <TechIcon slug="kubernetes" className="size-3.5 shrink-0" />
                <MonoNum muted>{cluster.version}</MonoNum>
              </InlineValue>
            </DetailRow>
            <DetailRow label={t('k8s.detail.field.cni')}>
              <Badge variant="outline">{cluster.cni}</Badge>
            </DetailRow>
            <DetailRow label={t('k8s.detail.field.fleet')}>
              <InlineValue>
                <MonoNum muted>{cluster.fleet}</MonoNum>
                <CopyButton value={cluster.fleet} copyLabel={t('table.copy.name')} />
              </InlineValue>
            </DetailRow>
            <DetailRow label={t('k8s.detail.field.endpoint')}>
              <CopyableText value={cluster.endpoint} copyLabel={t('table.copy.endpoint')}>
                <MonoNum muted>{cluster.endpoint}</MonoNum>
              </CopyableText>
            </DetailRow>
            <DetailRow label={t('k8s.detail.field.createdAt')}>
              {new Date(cluster.createdAt).toLocaleString()}
            </DetailRow>
          </CardContent>
        </Card>

        <div className="flex flex-col gap-4">
          <Card>
            <CardHeader className="border-b border-border">
              <CardTitle className="text-sm">{t('k8s.detail.section.capacity')}</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-2">
              <DetailRow label={t('k8s.detail.field.cpNodes')}>
                <MonoNum>{cluster.cpNodes}</MonoNum>
              </DetailRow>
              <DetailRow label={t('k8s.detail.field.workerNodes')}>
                <MonoNum>{cluster.workerNodes}</MonoNum>
              </DetailRow>
              <DetailRow label={t('k8s.detail.field.totalNodes')}>
                <MonoNum>{cluster.cpNodes + cluster.workerNodes}</MonoNum>
              </DetailRow>
              <DetailRow label={t('k8s.detail.field.vcpu')}>
                <MonoNum>{cluster.vcpu}</MonoNum>
              </DetailRow>
              <DetailRow label={t('k8s.detail.field.ram')}>
                <Size bytes={cluster.ramBytes} />
              </DetailRow>
            </CardContent>
          </Card>
        </div>
      </div>
    </PageTemplate>
  );
}

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
