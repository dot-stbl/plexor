import { createFileRoute } from '@tanstack/react-router';
import { Plus, Stack } from '@phosphor-icons/react';
import { Button } from '@/shared/ui/primitives/button';
import { PageHeader } from '@/shared/ui/app-shell';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { ClusterCard, useListClusters } from '@/features/clusters';

export const Route = createFileRoute('/clusters')({
  component: ClustersPage,
});

function ClustersPage() {
  const { clusters } = useListClusters();
  const totalVms = clusters.reduce((n, c) => n + c.vmCount, 0);
  const totalNodes = clusters.reduce((n, c) => n + c.nodeCount, 0);

  return (
    <main data-od-id="clusters-list">
      <PageHeader
        title="Вычислительные кластеры"
        description={
          <>
            <MonoNum>{clusters.length}</MonoNum> <span className="text-muted-foreground">кластер(ов) ·</span>{' '}
            <MonoNum>{totalNodes}</MonoNum> <span className="text-muted-foreground">нод ·</span>{' '}
            <MonoNum>{totalVms}</MonoNum> <span className="text-muted-foreground">VM</span>
          </>
        }
        actions={
          <Button>
            <Plus />
            Создать кластер
          </Button>
        }
      />

      <div className="mx-auto w-full max-w-6xl px-6 py-6 lg:px-8">
        {clusters.length === 0 ? (
          <EmptyClusters />
        ) : (
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
            {clusters.map((cluster) => (
              <ClusterCard key={cluster.id} cluster={cluster} />
            ))}
          </div>
        )}
      </div>
    </main>
  );
}

function EmptyClusters() {
  return (
    <div
      data-od-id="clusters-empty"
      className="flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-border bg-card/50 p-12 text-center"
    >
      <span className="flex size-10 items-center justify-center rounded-md bg-muted text-muted-foreground">
        <Stack className="size-5" />
      </span>
      <div className="space-y-0.5">
        <h3 className="text-sm font-medium">Кластеров пока нет</h3>
        <p className="text-xs text-muted-foreground">
          Создайте кластер, чтобы начать размещать ВМ.
        </p>
      </div>
      <Button size="sm">
        <Plus />
        Создать кластер
      </Button>
    </div>
  );
}