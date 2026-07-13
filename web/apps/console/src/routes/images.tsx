import { createFileRoute } from '@tanstack/react-router';
import { useLocalStorage } from '@uidotdev/usehooks';
import { toast } from 'sonner';
import { Plus, Image } from '@/shared/ui/icon';
import { Button } from '@/shared/ui/primitives/button';
import { PageTemplate } from '@/shared/ui/app-shell';
import { DataTable, DataTableColumns, type DataTableColumnsState } from '@/shared/ui/data-table';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { imageColumns, listImages } from '@/features/images';

export const Route = createFileRoute('/images')({
  staticData: { crumb: 'Images' },
  component: ImagesPage,
});

/**
 * Каталог образов — глобальный (кросс-секционный) ресурс: диск-шаблоны для
 * создания ВМ. Full-width список + column-manager; создание — глобальная
 * кнопка бара / CTA в шапке (пока заглушка).
 */
function ImagesPage() {
  const images = listImages();
  const [colState, setColState] = useLocalStorage<DataTableColumnsState>('plexor-cols-images', {
    hidden: [],
    order: [],
  });

  const createCta = (
    <Button size="sm" onClick={() => toast('Creating image — coming soon')}>
      <Plus />
      Create image
    </Button>
  );

  return (
    <PageTemplate
      data-od-id="images-list"
      width="full"
      title="Images"
      description={
        <>
          <MonoNum>{images.length}</MonoNum> <span className="text-muted-foreground">image(s)</span>
        </>
      }
      actions={images.length > 0 ? createCta : undefined}
    >
      {images.length > 0 ? (
        <div className="space-y-2">
          <div className="flex justify-end">
            <DataTableColumns columns={imageColumns} value={colState} onChange={setColState} />
          </div>
          <DataTable
            columns={imageColumns}
            data={images}
            density="compact"
            hiddenColumns={new Set(colState.hidden)}
            columnOrder={colState.order}
          />
        </div>
      ) : (
        <EmptyState
          icon={Image}
          title="No images yet"
          description="An image is a disk template that a VM is created from. Upload your own (qcow2/raw) or use public distributions."
          docsLabel="Learn more in the docs:"
          docs={[
            { href: 'https://plexor.dev/docs/images', label: 'Images and custom builds' },
            { href: 'https://plexor.dev/docs/images/upload', label: 'Uploading your own image' },
          ]}
          action={createCta}
        />
      )}
    </PageTemplate>
  );
}
