import { createFileRoute, Link } from '@tanstack/react-router';
import { useLocalStorage } from '@uidotdev/usehooks';
import { Add, DeployedCode } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { PageTemplate } from '@/shared/ui/app-shell';
import { DataTable, DataTableColumns, type DataTableColumnsState } from '@/shared/ui/data-table';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { lxcColumns, listLxc } from '@/features/lxc';

export const Route = createFileRoute('/lxc/')({
  component: LxcPage,
});

/**
 * LXC container catalog — full-width list + column-manager. Creation flows
 * from the header CTA / empty-state action, both linking to the create form.
 */
function LxcPage() {
  const rows = listLxc();
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>('plexor-cols-lxc', {
    hidden: [],
    order: [],
  });

  return (
    <PageTemplate
      data-od-id="lxc-list"
      width="full"
      title="LXC containers"
      description={
        <>
          <MonoNum>{rows.length}</MonoNum>{' '}
          <span className="text-muted-foreground">container(s)</span>
        </>
      }
      actions={
        rows.length > 0 ? (
          <Button nativeButton={false} render={<Link to="/lxc/new" />}>
            <Add />
            Create container
          </Button>
        ) : undefined
      }
    >
      {rows.length > 0 ? (
        <div className="space-y-2">
          <div className="flex justify-end">
            <DataTableColumns columns={lxcColumns} value={colState} onChange={setColState} />
          </div>
          <DataTable
            columns={lxcColumns}
            data={rows}
            density="compact"
            hiddenColumns={new Set(colState.hidden)}
            columnOrder={colState.order}
          />
        </div>
      ) : (
        <EmptyState
          icon={DeployedCode}
          title="No containers yet"
          description="LXC containers are lightweight system containers sharing the host kernel — fast to boot, dense on the node."
          docsLabel="Learn more:"
          docs={[
            { href: 'https://plexor.dev/docs/lxc', label: 'How LXC works' },
            { href: 'https://plexor.dev/docs/runtimes', label: 'Runtimes and placement' },
          ]}
          action={
            <Button nativeButton={false} render={<Link to="/lxc/new" />}>
              <Add className="size-3.5" />
              Create container
            </Button>
          }
        />
      )}
    </PageTemplate>
  );
}
