import { createFileRoute, Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { Add, Stacks } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { PageTemplate } from '@/shared/ui/app-shell';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { ClusterCard, useListClusters, countNodes } from '@/features/clusters';
import { routeHead } from '@/shared/lib/route-head';

export const Route = createFileRoute('/clusters/')({
  component: ClustersPage,
  ...routeHead('Clusters'),
});

function ClustersPage() {
  const { t } = useTranslation();
  const { clusters } = useListClusters();
  const totals = clusters.reduce(
    (acc, c) => {
      const n = countNodes(c.nodes);
      acc.clusters += 1;
      acc.nodes += n.total;
      acc.ready += n.ready;
      return acc;
    },
    { clusters: 0, nodes: 0, ready: 0 },
  );

  return (
    <PageTemplate
      title={t('clusters.list.title')}
      width="full"
      data-od-id="clusters-list"
      description={t('clusters.list.summary', {
        clusters: totals.clusters,
        ready: totals.ready,
        total: totals.nodes,
      })}
      actions={
        <Button nativeButton={false} render={<Link to="/clusters" />}>
          <Add />
          {t('clusters.list.docs')}
        </Button>
      }
    >
      {clusters.length === 0 ? (
        <EmptyClusters />
      ) : (
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
          {clusters.map((cluster) => (
            <ClusterCard key={cluster.id} cluster={cluster} />
          ))}
        </div>
      )}
    </PageTemplate>
  );
}

function EmptyClusters() {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="clusters-empty"
      icon={Stacks}
      title={t('clusters.list.empty.title')}
      description={t('clusters.list.empty.description', { code: 'plx init' })}
      docs={[{ href: 'https://plexor.dev/docs/install', label: t('clusters.list.empty.docsLabel') }]}
      action={
        <Button nativeButton={false} render={<Link to="/clusters" />}>
          <Add />
          {t('clusters.list.empty.docsLabel')}
        </Button>
      }
    />
  );
}