import type { Meta, StoryObj } from '@storybook/react-vite';
import { useTranslation } from 'react-i18next';
import { Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import type { ImageStatus, OsImage } from '@/domains/compute';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { DataTable, DataTableToolbar, type FilterValues } from '@/shared/ui/data-table';
import {
  ImageListBody,
  ImageStatusStrip,
  countImageByStatusFacet,
  getImageColumns,
  imageStatusLabelKey,
  sumImageTotals,
} from '@/domains/compute';

/**
 * /images list page stories.
 *
 * Renders `ImageListBody` (the whole page body incl. the status strip) with a
 * deterministic fixture catalog so baselines never depend on the handmade
 * mock module. `StatusChipActive` and `NoResults` render the controlled strip
 * + table directly — the body owns its filter state internally, so pre-set
 * filter states are shown through the strip's controlled props instead.
 */

const GIB = 1024 ** 3;

const CATALOG: OsImage[] = [
  {
    id: 'img-ubuntu-2404',
    name: 'ubuntu-24.04-lts',
    family: 'ubuntu',
    os: 'Ubuntu',
    version: '24.04 LTS',
    arch: 'x86_64',
    sizeBytes: Math.round(2.6 * GIB),
    minDiskBytes: 8 * GIB,
    visibility: 'public',
    status: 'ready',
    createdAt: '2026-05-02T00:00:00Z',
    techSlug: 'ubuntu',
    description: 'Noble Numbat. LTS until 2029.',
  },
  {
    id: 'img-ubuntu-2404-arm',
    name: 'ubuntu-24.04-lts-arm64',
    family: 'ubuntu',
    os: 'Ubuntu',
    version: '24.04 LTS',
    arch: 'arm64',
    sizeBytes: Math.round(2.5 * GIB),
    minDiskBytes: 8 * GIB,
    visibility: 'public',
    status: 'ready',
    createdAt: '2026-05-02T00:00:00Z',
    techSlug: 'ubuntu',
    description: 'Noble Numbat for ARM nodes.',
  },
  {
    id: 'img-debian-12',
    name: 'debian-12-bookworm',
    family: 'debian',
    os: 'Debian',
    version: '12 (bookworm)',
    arch: 'x86_64',
    sizeBytes: Math.round(1.9 * GIB),
    minDiskBytes: 6 * GIB,
    visibility: 'public',
    status: 'ready',
    createdAt: '2026-03-20T00:00:00Z',
    techSlug: 'debian',
    description: 'Stable Debian, minimal cloud image.',
  },
  {
    id: 'img-rocky-9',
    name: 'rocky-9.4',
    family: 'rocky',
    os: 'Rocky Linux',
    version: '9.4',
    arch: 'x86_64',
    sizeBytes: Math.round(1.7 * GIB),
    minDiskBytes: 10 * GIB,
    visibility: 'public',
    status: 'ready',
    createdAt: '2026-04-10T00:00:00Z',
    techSlug: 'rocky',
    description: 'RHEL-compatible enterprise distribution.',
  },
  {
    id: 'img-app-base-v3',
    name: 'app-base-v3',
    family: 'custom',
    os: 'Custom build',
    version: 'Debian 12 + app',
    arch: 'x86_64',
    sizeBytes: Math.round(4.2 * GIB),
    minDiskBytes: 16 * GIB,
    visibility: 'private',
    status: 'ready',
    createdAt: '2026-07-05T09:30:00Z',
    description: 'Golden image: Debian 12 + application runtime and agents.',
  },
  {
    id: 'img-gateway',
    name: 'gateway-appliance',
    family: 'custom',
    os: 'Custom build',
    version: 'Ubuntu 24.04 + net',
    arch: 'arm64',
    sizeBytes: Math.round(2.0 * GIB),
    minDiskBytes: 8 * GIB,
    visibility: 'private',
    status: 'ready',
    createdAt: '2026-07-12T11:00:00Z',
    description: 'Ubuntu + routing stack for network appliances.',
  },
  {
    id: 'img-ci-runner',
    name: 'ci-runner',
    family: 'custom',
    os: 'Custom build',
    version: 'Ubuntu 24.04 + CI',
    arch: 'x86_64',
    sizeBytes: Math.round(3.1 * GIB),
    minDiskBytes: 20 * GIB,
    visibility: 'private',
    status: 'creating',
    createdAt: '2026-07-09T06:15:00Z',
    description: 'Ubuntu + toolchain for CI runners.',
  },
  {
    id: 'img-ml-base',
    name: 'ml-base',
    family: 'custom',
    os: 'Custom build',
    version: 'Ubuntu 24.04 + CUDA',
    arch: 'x86_64',
    sizeBytes: Math.round(9.4 * GIB),
    minDiskBytes: 40 * GIB,
    visibility: 'private',
    status: 'creating',
    createdAt: '2026-09-18T08:00:00Z',
    description: 'Ubuntu + CUDA toolkit for ML workloads.',
  },
];

const noop = () => {};

/** Shared page frame for the controlled-strip stories (mirrors ImageListBody's). */
function StripStoryFrame({ activeStatus, children }: { activeStatus: ImageStatus | null; children?: React.ReactNode }) {
  const { t } = useTranslation();
  const columns = getImageColumns(t);
  const counts = countImageByStatusFacet(CATALOG);
  const visible = activeStatus ? CATALOG.filter((image) => image.status === activeStatus) : CATALOG;
  const filters: FilterValues = { name: '', arch: '', visibility: '', status: activeStatus ?? '' };
  const ready = counts.ready;

  return (
    <PageTemplate
      data-od-id="images-list"
      title={t('images.title')}
      width="wide"
      description={
        <span>
          <MonoNum>{ready}</MonoNum> <span className="text-muted-foreground">{t('images.list.readyOf')}</span>{' '}
          <MonoNum>{CATALOG.length}</MonoNum> <span className="text-muted-foreground">{t('images.list.total')}</span>
        </span>
      }
      actions={
        <Button>
          <Add />
          {t('images.create')}
        </Button>
      }
    >
      <div className="space-y-2">
        <ImageStatusStrip
          counts={counts}
          activeStatus={activeStatus}
          onToggleStatus={noop}
          totals={sumImageTotals(visible)}
        />
        <DataTableToolbar columns={columns} filters={filters} onFiltersChange={noop} />
        <DataTable columns={columns} data={visible} density="compact" />
        {children}
      </div>
    </PageTemplate>
  );
}

/** Table filtered to the active chip — same composition the body produces. */
function StatusChipActiveBody() {
  return <StripStoryFrame activeStatus="creating" />;
}

/** Active chip on a status with zero matches — strip + no-results state. */
function NoResultsBody() {
  const { t } = useTranslation();
  return (
    <StripStoryFrame activeStatus="error">
      <p className="py-6 text-center text-sm text-muted-foreground">
        {t('images.list.empty.noResultsStatusTitle', { status: t(imageStatusLabelKey('error')) })}
      </p>
    </StripStoryFrame>
  );
}

const meta = {
  title: 'Pages/Images',
  parameters: { layout: 'fullscreen' },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

/** Default: catalog with mixed statuses, strip chips + toolbar + table. */
export const Default: Story = {
  render: () => <ImageListBody items={CATALOG} onCreate={noop} />,
};

/** Empty catalog: no strip, empty state with a create CTA. */
export const Empty: Story = {
  render: () => <ImageListBody items={[]} onCreate={noop} />,
};

/** Loading: strip + table skeletons while the catalog resolves. */
export const Loading: Story = {
  render: () => <ImageListBody items={[]} isPending onCreate={noop} />,
};

/** One chip active: building chip emphasized, table filtered to building images. */
export const StatusChipActive: Story = {
  render: () => <StatusChipActiveBody />,
};

/** No results: error chip active with zero error images — status-aware empty state. */
export const NoResults: Story = {
  render: () => <NoResultsBody />,
};
