import { createFileRoute, Link } from '@tanstack/react-router';
import { Add, Stacks } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { PageTemplate } from '@/shared/ui/app-shell';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { ClusterCard, useListClusters, countNodes } from '@/features/clusters';

export const Route = createFileRoute('/clusters/')({
  component: ClustersPage,
});

function ClustersPage() {
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
      title="Кластеры Plexor"
      width="full"
      data-od-id="clusters-list"
      description={
        <>
          <MonoNum>{totals.clusters}</MonoNum> <span className="text-muted-foreground">кластер(ов) ·</span>{' '}
          <MonoNum>{totals.ready}</MonoNum>/<MonoNum>{totals.nodes}</MonoNum>{' '}
          <span className="text-muted-foreground">нод(ов) ready</span>
        </>
      }
      actions={
        <Button size="sm" nativeButton={false} render={<Link to="/clusters" />}>
          <Add />
          Документация
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
  return (
    <div
      data-od-id="clusters-empty"
      className="flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-border bg-card/50 p-12 text-center"
    >
      <span className="flex size-10 items-center justify-center rounded-md bg-muted text-muted-foreground">
        <Stacks className="size-5" />
      </span>
      <div className="space-y-0.5">
        <h3 className="text-sm font-medium">Нет зарегистрированных кластеров</h3>
        <p className="max-w-sm text-xs text-muted-foreground">
          Установите Plexor на своём сервере через{' '}
          <code className="rounded bg-muted px-1 font-mono text-[11px]">plx init</code>{' '}
          или ISO-образ, затем зарегистрируйте control-plane здесь.
        </p>
      </div>
      <Button size="sm" nativeButton={false} render={<Link to="/clusters" />}>
        <Add />
        Документация по установке
      </Button>
    </div>
  );
}