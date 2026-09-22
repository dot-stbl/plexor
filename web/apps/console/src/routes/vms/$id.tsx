import { useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useQueryClient } from '@tanstack/react-query';
import {
  ArrowBack,
  Delete,
  PlayArrow,
  Stop
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import { toast } from 'sonner';
import {
  useDeleteVm,
  useGetVm,
  useStartVm,
  useStopVm
} from '@/shared/api';
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
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { Alert, AlertDescription, AlertTitle } from '@/shared/ui/primitives/alert';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/shared/ui/primitives/alert-dialog';
import { mapVmStatusToVariant } from '@/features/vms';
import { useDocumentTitle } from '@/shared/lib/use-document-title';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/vms/$id')({
  component: VmDetailPage,
  ...routeHead('VM'),
});

/**
 * Single-VM detail screen.
 *
 * Data: kubb's `useGetVm(id)` returns a `VmDetail` (Vm + project/vpc/subnet/
 * image/security groups/encrypted/publicIp/updatedAt). Lifecycle controls are
 * derived from `vm.status` — no extra props required for that mapping.
 *
 * Placement context note: the brief mentions "cluster + node it lives on",
 * but the `VmDetail` schema doesn't carry a clusterId/nodeId — only
 * project/vpc/subnet/image. We surface those honestly; wiring cluster/node
 * here would invent a link the contract doesn't provide.
 *
 * On successful delete we invalidate the list query and navigate back to
 * `/vms` so the row disappears. Start/stop invalidate the detail + list
 * so the status pill flips without a manual refresh.
 */
function VmDetailPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { id } = Route.useParams();
  const { data: vm, isPending, isError, error, refetch } = useGetVm(id);
  const startMut = useStartVm();
  const stopMut = useStopVm();
  const deleteMut = useDeleteVm();
  const [deleteOpen, setDeleteOpen] = useState(false);

  useDocumentTitle(vm?.name ?? null);

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: [{ url: '/vms/:vmId', params: { vmId: id } }] });
    void queryClient.invalidateQueries({ queryKey: [{ url: '/vms' }] });
  };

  const onStart = () => {
    if (!vm) return;
    toast(t('vms.detail.lifecycle.startedToast', { name: vm.name }));
    startMut.mutate(
      { vmId: vm.id },
      {
        onSuccess: () => invalidate(),
        onError: () => toast.error(t('vms.detail.lifecycle.startFailed')),
      },
    );
  };

  const onStop = () => {
    if (!vm) return;
    toast(t('vms.detail.lifecycle.stoppedToast', { name: vm.name }));
    stopMut.mutate(
      { vmId: vm.id },
      {
        onSuccess: () => invalidate(),
        onError: () => toast.error(t('vms.detail.lifecycle.stopFailed')),
      },
    );
  };

  const onConfirmDelete = () => {
    if (!vm) return;
    setDeleteOpen(false);
    const name = vm.name;
    toast(t('vms.detail.lifecycle.deletedToast', { name }));
    deleteMut.mutate(
      { vmId: vm.id },
      {
        onSuccess: () => {
          void queryClient.invalidateQueries({ queryKey: [{ url: '/vms' }] });
          toast.success(t('vms.detail.lifecycle.deleteSuccessToast', { name }));
          void navigate({ to: '/vms' });
        },
        onError: () => toast.error(t('vms.detail.lifecycle.deleteFailed')),
      },
    );
  };

  if (isPending) {
    return (
      <PageTemplate
        title={t('vms.detail.title')}
        width="wide"
        data-od-id="vm-detail-skeleton"
        actions={
          <Button variant="ghost" nativeButton={false} render={<Link to="/vms" />}>
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

  if (isError || !vm) {
    const notFound =
      isError &&
      typeof error === 'object' &&
      error !== null &&
      'status' in error &&
      (error as { status?: number }).status === 404;
    return (
      <PageTemplate
        title={t('vms.detail.notFound')}
        width="default"
        data-od-id="vm-detail-not-found"
        actions={
          <Button variant="ghost" nativeButton={false} render={<Link to="/vms" />}>
            <ArrowBack />
            {t('vms.detail.back')}
          </Button>
        }
      >
        <Alert variant="destructive">
          <AlertTitle>{t('vms.detail.notFound')}</AlertTitle>
          <AlertDescription>
            {notFound
              ? t('vms.detail.notFoundDescription', { id })
              : error instanceof Error
                ? error.message
                : ''}
          </AlertDescription>
        </Alert>
        <div className="mt-3">
          <Button variant="outline" size="sm" onClick={() => void refetch()}>
            {t('common.retry')}
          </Button>
        </div>
      </PageTemplate>
    );
  }

  const isProvisioning = vm.status === 'provisioning';
  const isErrorState = vm.status === 'error';
  const canStart = vm.status === 'stopped' || vm.status === 'idle' || isErrorState;
  const canStop = vm.status === 'running' || vm.status === 'idle';

  return (
    <>
      <div data-od-id="vm-detail">
        <PageTemplate
          title={vm.name}
          width="wide"
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
              <Button variant="ghost" nativeButton={false} render={<Link to="/vms" />}>
                <ArrowBack />
                {t('vms.detail.back')}
              </Button>
              <Button variant="outline" onClick={onStart} disabled={!canStart || startMut.isPending}>
                <PlayArrow />
                {t('vms.detail.lifecycle.start')}
              </Button>
              <Button variant="outline" onClick={onStop} disabled={!canStop || stopMut.isPending}>
                <Stop />
                {t('vms.detail.lifecycle.stop')}
              </Button>
              <Button
                variant="destructive"
                onClick={() => setDeleteOpen(true)}
                disabled={deleteMut.isPending}
              >
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
                <DetailRow label={t('vms.detail.field.name')}>{vm.name}</DetailRow>
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
                <DetailRow label={t('vms.detail.field.image')}>{vm.image}</DetailRow>
                <DetailRow label={t('vms.detail.field.machineType')}>{vm.machineType}</DetailRow>
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
                  <DetailRow label={t('vms.detail.field.project')}>{vm.project}</DetailRow>
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
                  <Button
                    size="sm"
                    onClick={onStart}
                    disabled={!canStart || startMut.isPending}
                  >
                    <PlayArrow />
                    {t('vms.detail.lifecycle.start')}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={onStop}
                    disabled={!canStop || stopMut.isPending}
                  >
                    <Stop />
                    {t('vms.detail.lifecycle.stop')}
                  </Button>
                  <Button
                    variant="destructive"
                    size="sm"
                    onClick={() => setDeleteOpen(true)}
                    disabled={deleteMut.isPending}
                  >
                    <Delete />
                    {t('vms.detail.lifecycle.delete')}
                  </Button>
                </CardContent>
              </Card>
            </div>
          </div>
        </PageTemplate>
      </div>

      <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <AlertDialogContent size="default">
          <AlertDialogHeader>
            <AlertDialogTitle>{t('vms.detail.lifecycle.deleteTitle')}</AlertDialogTitle>
            <AlertDialogDescription>
              {t('vms.detail.lifecycle.deleteDescription')}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={onConfirmDelete}>
              {t('common.delete')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
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
