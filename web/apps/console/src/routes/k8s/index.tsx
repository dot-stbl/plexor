import { createFileRoute, Link } from '@tanstack/react-router';
import { useLocalStorage } from '@uidotdev/usehooks';
import { Add, Hexagon } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { PageTemplate } from '@/shared/ui/app-shell';
import { DataTable, DataTableColumns, type DataTableColumnsState } from '@/shared/ui/data-table';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { k8sColumns, listK8s } from '@/features/k8s';

export const Route = createFileRoute('/k8s/')({
  component: K8sPage,
});

/**
 * Managed Kubernetes (K3s) clusters — full-width list + column-manager;
 * creation flows from the header CTA / empty-state action.
 */
function K8sPage() {
  const rows = listK8s();
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>('plexor-cols-k8s', {
    hidden: [],
    order: [],
  });

  return (
    <PageTemplate
      data-od-id="k8s-list"
      width="full"
      title="Kubernetes clusters"
      description={
        <>
          <MonoNum>{rows.length}</MonoNum> <span className="text-muted-foreground">cluster(s)</span>
        </>
      }
      actions={
        rows.length > 0 ? (
          <Button nativeButton={false} render={<Link to="/k8s/new" />}>
            <Add />
            Create cluster
          </Button>
        ) : undefined
      }
    >
      {rows.length > 0 ? (
        <div className="space-y-2">
          <div className="flex justify-end">
            <DataTableColumns columns={k8sColumns} value={colState} onChange={setColState} />
          </div>
          <DataTable
            columns={k8sColumns}
            data={rows}
            density="compact"
            hiddenColumns={new Set(colState.hidden)}
            columnOrder={colState.order}
          />
        </div>
      ) : (
        <EmptyState
          icon={Hexagon}
          title="No Kubernetes clusters yet"
          description="Plexor provisions managed K3s across your nodes — you pick node pools, CNI and storage; Plexor wires the rest."
          docsLabel="Learn more:"
          docs={[
            { href: 'https://plexor.dev/docs/k8s', label: 'Managed Kubernetes (K3s)' },
            { href: 'https://plexor.dev/docs/k8s/node-pools', label: 'Node pools and placement' },
          ]}
          action={
            <Button nativeButton={false} render={<Link to="/k8s/new" />}>
              <Add className="size-3.5" />
              Create cluster
            </Button>
          }
        />
      )}
    </PageTemplate>
  );
}
