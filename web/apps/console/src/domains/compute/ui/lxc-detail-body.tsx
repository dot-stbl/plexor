import { useTranslation } from 'react-i18next';
import { ArrowBack, DeployedCode } from '@nine-thirty-five/material-symbols-react/rounded/700';
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
import type { LxcContainer } from '../model/lxc-types';
import { mapLxcStatusToVariant } from '../model/lxc-types';
import { lxcStatusLabelKey } from './lxc-status-strip';

interface LxcDetailBodyProps {
  /** The container to render. */
  container: LxcContainer;
  /** Navigate back to /lxc. */
  onBack: () => void;
}

/**
 * /lxc/$id page body: identity, resources and placement cards over one LXC
 * system container. Sizes are exact binary bytes (`Size` picks the unit);
 * the template family maps to a distro slug for the TechIcon.
 *
 * Honest header: only Back. The container has a lifecycle in the data
 * (running/stopped/paused), but the handmade mock is read-only and the
 * contract has no start/stop endpoints — mutation buttons here would be
 * fake toasts, so none are rendered.
 */
export function LxcDetailBody({ container, onBack }: LxcDetailBodyProps) {
  const { t } = useTranslation();
  const templateFamily = container.template.split('-')[0];

  return (
    <PageTemplate
      data-od-id="lxc-detail"
      title={container.name}
      width="wide"
      description={
        <span className="inline-flex items-center gap-2">
          <StatusPill variant={mapLxcStatusToVariant(container.status)} size="sm">
            {t(lxcStatusLabelKey(container.status))}
          </StatusPill>
          <span className="inline-block h-3 w-px bg-border" aria-hidden />
          <span className="text-muted-foreground">{t('lxc.detail.subtitle')}</span>
        </span>
      }
      actions={
        <Button variant="ghost" onClick={onBack}>
          <ArrowBack />
          {t('lxc.detail.back')}
        </Button>
      }
    >
      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader className="border-b border-border">
            <CardTitle className="text-sm">{t('lxc.detail.section.identity')}</CardTitle>
            <CardDescription>{container.id}</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            <DetailRow label={t('lxc.detail.field.name')}>
              <InlineValue>
                {container.name}
                <CopyButton value={container.name} copyLabel={t('table.copy.name')} />
              </InlineValue>
            </DetailRow>
            <DetailRow label={t('lxc.detail.field.id')}>
              <CopyableText value={container.id} copyLabel={t('table.copy.id')}>
                <MonoNum muted>{container.id}</MonoNum>
              </CopyableText>
            </DetailRow>
            <DetailRow label={t('lxc.detail.field.status')}>
              <StatusPill variant={mapLxcStatusToVariant(container.status)} size="sm">
                {t(lxcStatusLabelKey(container.status))}
              </StatusPill>
            </DetailRow>
            <DetailRow label={t('lxc.detail.field.os')}>
              <InlineValue>
                <TechIcon slug={templateFamily} fallback={DeployedCode} className="size-3.5 shrink-0" />
                {container.os}
                <MonoNum muted>{container.osVersion}</MonoNum>
              </InlineValue>
            </DetailRow>
            <DetailRow label={t('lxc.detail.field.template')}>
              <InlineValue>
                <MonoNum muted>{container.template}</MonoNum>
                <CopyButton value={container.template} copyLabel={t('table.copy.template')} />
              </InlineValue>
            </DetailRow>
            <DetailRow label={t('lxc.detail.field.type')}>
              <Badge variant={container.unprivileged ? 'secondary' : 'outline'}>
                {container.unprivileged ? 'unprivileged' : 'privileged'}
              </Badge>
            </DetailRow>
            <DetailRow label={t('lxc.detail.field.createdAt')}>
              {new Date(container.createdAt).toLocaleString()}
            </DetailRow>
          </CardContent>
        </Card>

        <div className="flex flex-col gap-4">
          <Card>
            <CardHeader className="border-b border-border">
              <CardTitle className="text-sm">{t('lxc.detail.section.resources')}</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-2">
              <DetailRow label={t('lxc.detail.field.cores')}>
                <MonoNum>{container.cores}</MonoNum>
              </DetailRow>
              <DetailRow label={t('lxc.detail.field.memory')}>
                <Size bytes={container.ramBytes} />
              </DetailRow>
              <DetailRow label={t('lxc.detail.field.rootfs')}>
                <Size bytes={container.rootfsBytes} muted />
              </DetailRow>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="border-b border-border">
              <CardTitle className="text-sm">{t('lxc.detail.section.placement')}</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-2">
              <DetailRow label={t('lxc.detail.field.node')}>
                <CopyableText value={container.nodeHostname} copyLabel={t('table.copy.host')}>
                  <MonoNum muted>{container.nodeHostname}</MonoNum>
                </CopyableText>
              </DetailRow>
              <DetailRow label={t('lxc.detail.field.ip')}>
                <CopyableText value={container.ip} copyLabel={t('table.copy.ip')}>
                  <MonoNum muted>{container.ip}</MonoNum>
                </CopyableText>
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
