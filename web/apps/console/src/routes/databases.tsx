import { useMemo, useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { Plus, Database } from '@phosphor-icons/react';
import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { PageHeader } from '@/shared/ui/app-shell';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/ui/primitives/tabs';
import { DataTable } from '@/shared/ui/data-table';
import type { DbEngine, DbInstance } from '@/features/databases';
import { dbColumns, useEngines, useListDbInstances } from '@/features/databases';

export const Route = createFileRoute('/databases')({
  component: DatabasesPage,
});

function DatabasesPage() {
  const { engines } = useEngines();
  const { instances } = useListDbInstances();
  const [tab, setTab] = useState<string>(engines[0]?.id ?? '');

  // Группируем инстансы по движку — у каждой вкладки своя таблица.
  const byEngine = useMemo(() => {
    const map: Record<string, DbInstance[]> = {};
    for (const db of instances) (map[db.engineId] ??= []).push(db);
    return map;
  }, [instances]);

  const total = instances.length;
  const running = useMemo(
    () => instances.reduce((n, db) => (db.status === 'running' ? n + 1 : n), 0),
    [instances],
  );

  return (
    <main data-od-id="databases-list">
      <PageHeader
        title="Платформа данных"
        description={
          <span>
            <MonoNum>{running}</MonoNum> <span className="text-muted-foreground">running of</span>{' '}
            <MonoNum>{total}</MonoNum> <span className="text-muted-foreground">· бэкапы, HA, тонкая настройка</span>
          </span>
        }
      />

      <div className="mx-auto w-full max-w-6xl px-6 py-6 lg:px-8">
        <Tabs value={tab} onValueChange={setTab}>
          <TabsList>
            {engines.map((engine) => (
              <TabsTrigger key={engine.id} value={engine.id}>
                {engine.name}
                <span className="ml-1 text-muted-foreground">({byEngine[engine.id]?.length ?? 0})</span>
              </TabsTrigger>
            ))}
          </TabsList>

          {engines.map((engine) => (
            <TabsContent key={engine.id} value={engine.id} className="min-w-0 space-y-3">
              <EngineSection engine={engine} rows={byEngine[engine.id] ?? []} />
            </TabsContent>
          ))}
        </Tabs>
      </div>
    </main>
  );
}

function EngineSection({ engine, rows }: { engine: DbEngine; rows: DbInstance[] }) {
  const hasRows = rows.length > 0;
  return (
    <>
      <div className="flex items-center justify-between gap-3">
        <p className="min-w-0 truncate text-xs text-muted-foreground">
          {engine.blurb} <span className="font-mono">· {engine.source}</span>
        </p>
        {hasRows && (
          <Button size="sm" className="shrink-0" nativeButton={false} render={<Link to="/databases/new" search={{ engine: engine.id }} />}>
            <Plus className="size-3.5" />
            Создать {engine.name}
          </Button>
        )}
      </div>

      {hasRows ? (
        <DataTable columns={dbColumns} data={rows} density="compact" />
      ) : (
        <div className="flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-border bg-card/50 p-10 text-center">
          <span className="flex size-10 items-center justify-center rounded-md bg-muted text-muted-foreground">
            <Database className="size-5" />
          </span>
          <div className="space-y-0.5">
            <h3 className="text-sm font-medium">Нет инстансов {engine.name}</h3>
            <p className="max-w-sm text-xs text-muted-foreground">{engine.blurb}</p>
          </div>
          <Button size="sm" nativeButton={false} render={<Link to="/databases/new" search={{ engine: engine.id }} />}>
            <Plus className="size-3.5" />
            Создать {engine.name}
          </Button>
        </div>
      )}
    </>
  );
}
