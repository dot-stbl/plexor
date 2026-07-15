import { Link } from '@tanstack/react-router';
import { useLocalStorage } from '@uidotdev/usehooks';
import { Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { PageTemplate } from '@/shared/ui/app-shell';
import { DataTable, DataTableColumns, type DataTableColumnsState } from '@/shared/ui/data-table';
import { dbColumns } from './database-columns';
import { useEngine, useListDbClusters } from './use-databases';
import { ManagedServiceEmpty } from './managed-service-empty';

/**
 * Страница одного managed-движка (раздел «Managed Service for X»): список
 * его кластеров + богатый онбординг, если их нет. Монтируется из route-файла
 * (/managed/<engine>) внутри layout-роута /managed через `<Outlet/>`. Чром —
 * через `PageTemplate` (крошки — в верхнем баре из staticData).
 */
export function ManagedServicePage({ engineId }: { engineId: string }) {
  const engine = useEngine(engineId);
  const { clusters } = useListDbClusters();
  const rows = clusters.filter((c) => c.engineId === engineId);
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>(
    `plexor-cols-managed-${engineId}`,
    { hidden: [], order: [] },
  );

  if (!engine) {
    return (
      <PageTemplate title="Engine not found">
        <p className="text-sm text-muted-foreground">Unknown engine: {engineId}.</p>
      </PageTemplate>
    );
  }

  return (
    <PageTemplate
      data-od-id={`managed-${engine.id}`}
      width="full"
      title={engine.name}
      description={engine.blurb}
      actions={
        rows.length > 0 ? (
          <Button nativeButton={false} render={<Link to="/managed/new" search={{ engine: engine.id }} />}>
            <Add className="size-3.5" />
            Create cluster
          </Button>
        ) : null
      }
    >
      {rows.length > 0 ? (
        <div className="space-y-2">
          <div className="flex justify-end">
            <DataTableColumns columns={dbColumns} value={colState} onChange={setColState} />
          </div>
          <DataTable
            columns={dbColumns}
            data={rows}
            density="compact"
            hiddenColumns={new Set(colState.hidden)}
            columnOrder={colState.order}
          />
        </div>
      ) : (
        <ManagedServiceEmpty engine={engine} />
      )}
    </PageTemplate>
  );
}
