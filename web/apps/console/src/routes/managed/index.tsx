import { useMemo } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { Add, Database } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useState } from 'react';
import { PageTemplate } from '@/shared/ui/app-shell';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Button } from '@/shared/ui/primitives/button';
import { DataTable } from '@/shared/ui/data-table';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/ui/primitives/tabs';
import {
  dbColumns,
  useEngines,
  useListDbClusters,
  type DbCluster,
  type DbEngine,
} from '@/features/databases';

export const Route = createFileRoute('/managed/')({
  staticData: { crumb: 'Data platform' },
  component: DataPlatformPage,
});

function DataPlatformPage() {
  const { engines } = useEngines();
  const { clusters } = useListDbClusters();
  const [tab, setTab] = useState<string>(engines[0]?.id ?? '');

  const byEngine = useMemo(() => {
    const map: Record<string, DbCluster[]> = {};
    for (const c of clusters) (map[c.engineId] ??= []).push(c);
    return map;
  }, [clusters]);

  const total = clusters.length;
  const running = useMemo(
    () => clusters.reduce((n, c) => (c.status === 'running' ? n + 1 : n), 0),
    [clusters],
  );

  return (
    <PageTemplate
      title="Платформа данных"
      width="full"
      data-od-id="managed-index"
      description={
        <span>
          <MonoNum>{running}</MonoNum>{' '}
          <span className="text-muted-foreground">running of</span>{' '}
          <MonoNum>{total}</MonoNum>{' '}
          <span className="text-muted-foreground">· бэкапы, HA, тонкая настройка</span>
        </span>
      }
    >
      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          {engines.map((engine) => (
            <TabsTrigger key={engine.id} value={engine.id}>
              {engine.name}
              <span className="ml-1 text-muted-foreground">
                ({byEngine[engine.id]?.length ?? 0})
              </span>
            </TabsTrigger>
          ))}
        </TabsList>

        {engines.map((engine) => (
          <TabsContent key={engine.id} value={engine.id} className="min-w-0 space-y-3">
            <EngineSection engine={engine} rows={byEngine[engine.id] ?? []} />
          </TabsContent>
        ))}
      </Tabs>
    </PageTemplate>
  );
}

function EngineSection({ engine, rows }: { engine: DbEngine; rows: DbCluster[] }) {
  const hasRows = rows.length > 0;

  if (!hasRows) {
    return (
      <EmptyState
        data-od-id={`managed-empty-${engine.id}`}
        icon={Database}
        title={`Нет инстансов ${engine.name}`}
        description={engine.blurb}
        action={
          <Button nativeButton={false} render={<Link to="/managed/new" search={{ engine: engine.id }} />}>
            <Add />
            Create {engine.name} cluster
          </Button>
        }
      />
    );
  }

  return (
    <>
      <div className="flex items-center justify-between gap-3">
        <p className="min-w-0 truncate text-xs text-muted-foreground">
          {engine.blurb} <span className="font-mono">· {engine.source}</span>
        </p>
        <Button nativeButton={false} render={<Link to="/managed/new" search={{ engine: engine.id }} />}>
          <Add className="size-3.5" />
          Create {engine.name} cluster
        </Button>
      </div>
      <DataTable columns={dbColumns} data={rows} density="compact" />
    </>
  );
}
