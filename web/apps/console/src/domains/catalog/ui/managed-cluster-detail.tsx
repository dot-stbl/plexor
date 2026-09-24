import { useTranslation } from 'react-i18next';
import { ArrowBack } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Alert, AlertDescription, AlertTitle } from '@/shared/ui/primitives/alert';
import { Badge } from '@/shared/ui/primitives/badge';
import { Button } from '@/shared/ui/primitives/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/shared/ui/primitives/card';
import { CopyableText } from '@/shared/ui/primitives/copyable-text';
import { CopyButton } from '@/shared/ui/primitives/copy-button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { TechIcon } from '@/shared/ui/primitives/tech-icon';
import type { DbCluster, DbEngine } from '../model/database-types';
import { DB_KIND_LABEL, mapDbStatusToVariant } from '../model/database-types';
import { DB_KIND_ICON } from './managed-service-empty';
import { RuntimeBadge } from './runtime-badge';
import { dbStatusLabelKey } from './db-status-strip';

interface ManagedClusterDetailProps {
  /** Engine catalog entry (resolved by the route shell). */
  engine: DbEngine;
  /** The deployed cluster (resolved by the route shell). */
  cluster: DbCluster;
  /** Navigate back to the engine section (/managed/<engine>). */
  onBack: () => void;
}

/**
 * DB cluster detail body (/managed/<engine>/c/<clusterId>), shaped after
 * /vms/$id: PageTemplate wide, identity / connection / storage cards.
 *
 * Lifecycle (start/stop) is deliberately absent: clusters come from a
 * synchronous catalog mock — there is no mutation endpoint even in MSW, so
 * action buttons would be pure theater. Revisit when the contract gains
 * POST /db-clusters/{id}:start|:stop.
 */
export function ManagedClusterDetail({ engine, cluster, onBack }: ManagedClusterDetailProps) {
  const { t } = useTranslation();

  return (
    <PageTemplate
      data-od-id={`managed-cluster-${cluster.id}`}
      width="wide"
      title={cluster.name}
      description={
        <span className="flex flex-wrap items-center gap-2">
          <StatusPill variant={mapDbStatusToVariant(cluster.status)} size="sm">
            {t(dbStatusLabelKey(cluster.status))}
          </StatusPill>
          <span className="inline-flex items-center gap-1.5">
            <TechIcon slug={engine.id} fallback={DB_KIND_ICON[engine.kind]} className="size-4" />
            <span>{engine.name}</span>
          </span>
          <MonoNum muted>v{cluster.version}</MonoNum>
          <RuntimeBadge runtime={cluster.runtime} />
        </span>
      }
      actions={
        <Button variant="ghost" onClick={onBack}>
          <ArrowBack />
          {t('managed.detail.back')}
        </Button>
      }
    >
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader className="border-b border-border">
            <CardTitle className="text-sm">{t('managed.detail.section.identity')}</CardTitle>
            <CardDescription>{cluster.id}</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            <DetailRow label={t('managed.detail.field.name')}>
              <InlineValue>
                {cluster.name}
                <CopyButton value={cluster.name} copyLabel={t('table.copy.name')} />
              </InlineValue>
            </DetailRow>
            <DetailRow label={t('managed.detail.field.id')}>
              <CopyableText value={cluster.id} copyLabel={t('table.copy.id')}>
                <MonoNum muted>{cluster.id}</MonoNum>
              </CopyableText>
            </DetailRow>
            <DetailRow label={t('managed.detail.field.engine')}>
              <InlineValue>
                <TechIcon slug={engine.id} fallback={DB_KIND_ICON[engine.kind]} className="size-3.5" />
                <span>{engine.name}</span>
              </InlineValue>
            </DetailRow>
            <DetailRow label={t('managed.detail.field.kind')}>
              <Badge variant="secondary">{DB_KIND_LABEL[cluster.kind]}</Badge>
            </DetailRow>
            <DetailRow label={t('table.status')}>
              <StatusPill variant={mapDbStatusToVariant(cluster.status)} size="sm">
                {t(dbStatusLabelKey(cluster.status))}
              </StatusPill>
            </DetailRow>
            <DetailRow label={t('table.version')}>
              <MonoNum>v{cluster.version}</MonoNum>
            </DetailRow>
            <DetailRow label={t('managed.detail.field.createdAt')}>
              {new Date(cluster.createdAt).toLocaleString()}
            </DetailRow>
          </CardContent>
        </Card>

        <div className="flex flex-col gap-4">
          <Card>
            <CardHeader className="border-b border-border">
              <CardTitle className="text-sm">{t('managed.detail.section.connection')}</CardTitle>
              <CardDescription>{t('managed.detail.section.connectionDescription')}</CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-2">
              <DetailRow label={t('table.internalDns')}>
                <CopyableText value={cluster.dns} copyLabel={t('table.copy.dns')}>
                  {cluster.dns}
                </CopyableText>
              </DetailRow>
              <DetailRow label={t('managed.detail.field.binding')}>
                <code className="font-mono text-xs text-foreground">{engine.connstring}</code>
              </DetailRow>
              <DetailRow label={t('table.node')}>
                <CopyableText value={cluster.hostname} copyLabel={t('table.copy.host')}>
                  {cluster.hostname}
                </CopyableText>
              </DetailRow>
              <DetailRow label={t('managed.detail.field.nodeId')}>
                <CopyableText value={cluster.nodeId} copyLabel={t('table.copy.id')}>
                  <MonoNum muted>{cluster.nodeId}</MonoNum>
                </CopyableText>
              </DetailRow>
              <DetailRow label={t('table.runtime')}>
                <RuntimeBadge runtime={cluster.runtime} />
              </DetailRow>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="border-b border-border">
              <CardTitle className="text-sm">{t('managed.detail.section.storage')}</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-2">
              <DetailRow label={t('table.disk')}>
                <MonoNum>{cluster.storageGb}</MonoNum>
                <span className="ml-0.5 text-muted-foreground">GB</span>
              </DetailRow>
              <DetailRow label={t('table.backups')}>
                {cluster.backupsEnabled ? t('common.yes') : t('common.no')}
              </DetailRow>
              <DetailRow label={t('table.bindings')}>
                <MonoNum>{cluster.bindings}</MonoNum>
              </DetailRow>
            </CardContent>
          </Card>
        </div>
      </div>
    </PageTemplate>
  );
}

interface ManagedClusterSkeletonProps {
  /** Navigate back to the engine section. */
  onBack: () => void;
}

/** Loading placeholder matching the detail layout (header + two card columns). */
export function ManagedClusterSkeleton({ onBack }: ManagedClusterSkeletonProps) {
  const { t } = useTranslation();
  return (
    <PageTemplate
      title={t('managed.detail.title')}
      width="wide"
      data-od-id="managed-cluster-skeleton"
      actions={
        <Button variant="ghost" onClick={onBack}>
          <ArrowBack />
          {t('managed.detail.back')}
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

interface ManagedClusterNotFoundProps {
  /** Cluster id from the URL (for the copy). */
  clusterId: string;
  /** Navigate back to the section landing. */
  onBack: () => void;
}

/** 404 for an unknown cluster id. */
export function ManagedClusterNotFound({ clusterId, onBack }: ManagedClusterNotFoundProps) {
  const { t } = useTranslation();
  return (
    <PageTemplate
      title={t('managed.detail.notFound.title')}
      width="default"
      data-od-id="managed-cluster-not-found"
      actions={
        <Button variant="ghost" onClick={onBack}>
          <ArrowBack />
          {t('managed.detail.back')}
        </Button>
      }
    >
      <Alert variant="destructive">
        <AlertTitle>{t('managed.detail.notFound.title')}</AlertTitle>
        <AlertDescription>
          {t('managed.detail.notFound.description', { id: clusterId })}
        </AlertDescription>
      </Alert>
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
