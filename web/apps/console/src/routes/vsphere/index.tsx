import { useMemo } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { Add, Refresh } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { PageTemplate } from '@/shared/ui/app-shell';
import { routeHead } from '@/shared/lib/route-head';
import { DataTable, emptyFilters, compactFilters, type ColumnDef, type FilterValues } from '@/shared/ui/data-table';
import {
  VSphereEmptyState,
  VSphereErrorBanner,
  VSphereInventorySkeleton,
  getVSphereClusterColumns,
  getVSphereHostColumns,
  getVSphereVmColumns,
  useVSphereInventory,
  useVSphereRefresh,
} from '@/domains/compute';
import type { VSphereInventoryClusterRow } from '@/shared/api';

export const Route = createFileRoute('/vsphere/')({
  component: VSphereInventoryPage,
  ...routeHead('vSphere inventory'),
});

/**
 * Renders three sections — clusters, hosts, VMs — each backed by
 * its own DataTable. The inventory GET returns 503 when the vSphere
 * provider isn't configured OR the cache is empty; both surface as
 * the same ProblemDetails body, so the screen shows the empty
 * state with a primary "Refresh now" button instead of an error
 * banner in that case.
 */
function VSphereInventoryPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { inventory, isPending, isError, error, refetch } = useVSphereInventory();
  const refresh = useVSphereRefresh();

  const clusterColumns = useMemo(() => getVSphereClusterColumns(t), [t]);
  const hostColumns = useMemo(() => getVSphereHostColumns(t), [t]);
  const vmColumns = useMemo(() => getVSphereVmColumns(t), [t]);

  const clusters = inventory?.clusters ?? [];
  const hosts = inventory?.hosts ?? [];
  const vms = inventory?.virtualMachines ?? [];

  // 503 is the kubb-typed error for this endpoint — it's also what the
  // mock returns when the inventory is empty. Treat as the empty state.
  const is503 = !isPending && isError;
  const isEmpty = !isPending && !isError && clusters.length === 0 && hosts.length === 0 && vms.length === 0;

  // Filter scaffolding — currently each table holds its own client-side
  // filter via the column meta; left wired for when server-side filtering
  // becomes a thing.
  const clusterFilters: FilterValues = emptyFilters(clusterColumns);
  const hostFilters: FilterValues = emptyFilters(hostColumns);
  const vmFilters: FilterValues = emptyFilters(vmColumns);
  void clusterFilters; void hostFilters; void vmFilters; void compactFilters;

  return (
    <PageTemplate
      data-od-id="vsphere-inventory-list"
      title={t('vsphere.inventory.title')}
      width="wide"
      description={
        isPending ? (
          t('common.loading')
        ) : inventory ? (
          <span>
            <span className="text-muted-foreground">{t('vsphere.inventory.summary')}</span>{' '}
            {clusters.length} · {hosts.length} · {vms.length}
            {inventory.snapshot.refreshedAt && (
              <span className="text-muted-foreground">
                {' '}
                · {new Date(inventory.snapshot.refreshedAt).toLocaleString()}
              </span>
            )}
          </span>
        ) : (
          undefined
        )
      }
      actions={
        <Button
          variant="outline"
          onClick={() => refresh.mutate()}
          disabled={refresh.isPending}
          data-od-id="vsphere-inventory-refresh"
        >
          <Refresh />
          {refresh.isPending ? t('vsphere.inventory.refreshing') : t('vsphere.inventory.refresh')}
        </Button>
      }
    >
      {isPending ? (
        <VSphereInventorySkeleton />
      ) : isError && !is503 ? (
        <VSphereErrorBanner error={error} onRetry={() => void refetch()} />
      ) : is503 || isEmpty ? (
        <VSphereEmptyState onRefresh={() => refresh.mutate()} />
      ) : (
        <div className="flex flex-col gap-6">
          <ResourceSection<VSphereInventoryClusterRow>
            title={t('vsphere.inventory.clustersTitle', { count: clusters.length })}
            columns={clusterColumns}
            data={clusters}
            emptyText={t('vsphere.inventory.clustersEmpty')}
          />
          <ResourceSection
            title={t('vsphere.inventory.hostsTitle', { count: hosts.length })}
            columns={hostColumns}
            data={hosts}
            emptyText={t('vsphere.inventory.hostsEmpty')}
          />
          <ResourceSection
            title={t('vsphere.inventory.vmsTitle', { count: vms.length })}
            columns={vmColumns}
            data={vms}
            emptyText={t('vsphere.inventory.vmsEmpty')}
            action={
              <Button
                variant="outline"
                size="sm"
                onClick={() => navigate({ to: '/vsphere/clone' })}
                data-od-id="vsphere-inventory-clone"
              >
                <Add />
                {t('vsphere.inventory.cloneCta')}
              </Button>
            }
          />
        </div>
      )}

      <div className="mt-6 flex items-center justify-between border-t border-border pt-4 text-xs text-muted-foreground">
        <Link to="/vsphere/clone">{t('vsphere.inventory.cloneCta')}</Link>
        <span>
          {t('vsphere.inventory.vcenterLabel')}{' '}
          <span className="font-mono">{inventory?.snapshot.vcenterMoref ?? '—'}</span>
        </span>
      </div>
    </PageTemplate>
  );
}

interface ResourceSectionProps<TData> {
  title: string;
  columns: ColumnDef<TData>[];
  data: TData[];
  emptyText: string;
  action?: React.ReactNode;
}

/** A single resource section — title + optional action button + table. */
function ResourceSection<TData>({ title, columns, data, emptyText, action }: ResourceSectionProps<TData>) {
  return (
    <section className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold tracking-tight">{title}</h2>
        {action}
      </div>
      {data.length === 0 ? (
        <p className="rounded-lg border border-dashed border-border bg-muted/30 px-4 py-6 text-center text-xs text-muted-foreground">
          {emptyText}
        </p>
      ) : (
        <DataTable columns={columns} data={data} density="compact" />
      )}
    </section>
  );
}
