import { Link } from '@tanstack/react-router';
import { Dialog } from '@base-ui/react/dialog';
import type { Icon } from '@phosphor-icons/react';
import { X, CaretRight, SquaresFour, BookOpen, GearSix, SlidersHorizontal } from '@phosphor-icons/react';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/shared/ui/primitives/card';
import { Button } from '@/shared/ui/primitives/button';
import { useSidebar } from '@/shared/ui/primitives/sidebar';
import { Stat } from '@/shared/ui/primitives/stat';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { cn } from '@/lib/utils';
import { SECTIONS, type AppRoute, type NavPage, type Section } from './nav-config';

type MetaHub = { name: string; caption: string; icon: Icon; to?: AppRoute; soon?: boolean };

/** Row 1 — cross-cutting entry hubs (4). */
const META: MetaHub[] = [
  { name: 'Обзор проекта', caption: 'Сводка и действия', icon: SquaresFour, to: '/' },
  { name: 'Документация', caption: 'API (Scalar) и гайды', icon: BookOpen, soon: true },
  { name: 'Администрирование', caption: 'Ноды, провайдеры', icon: GearSix, soon: true },
  { name: 'Настройки', caption: 'Профиль, ключи, тема', icon: SlidersHorizontal, soon: true },
];

/** Row 2 — at-a-glance summary (3). No backend yet → honest empty state. */
const SUMMARY: { label: string; to: AppRoute }[] = [
  { label: 'Виртуальные машины', to: '/vms' },
  { label: 'Сети · VPC', to: '/networks' },
  { label: 'События аудита', to: '/audit' },
];

const linkRing = 'block rounded-lg outline-none focus-visible:ring-2 focus-visible:ring-ring/40';
// Inset tile: bg-muted so it reads against the top region's big bg-card.
const tile = 'rounded-lg bg-muted/60 transition-colors';

const SoonTag = () => (
  <StatusPill variant="idle" hideDot className="shrink-0 px-1.5 py-0 text-[9.5px] font-normal">
    скоро
  </StatusPill>
);

function MetaCard({ hub, onNavigate }: { hub: MetaHub; onNavigate: () => void }) {
  const HubIcon = hub.icon;
  const inner = (
    <div className={cn('flex h-full items-center gap-2.5 px-3 py-2.5', tile, hub.to ? 'hover:bg-muted' : 'opacity-60')}>
      <span className="flex size-8 shrink-0 items-center justify-center rounded-md bg-background text-foreground">
        <HubIcon className="size-4" />
      </span>
      <div className="min-w-0">
        <div className="flex items-center gap-1.5">
          <span className="truncate text-xs font-medium">{hub.name}</span>
          {hub.soon && <SoonTag />}
        </div>
        <div className="truncate text-[10.5px] text-muted-foreground">{hub.caption}</div>
      </div>
    </div>
  );
  return hub.to ? (
    <Link to={hub.to} onClick={onNavigate} className={linkRing}>
      {inner}
    </Link>
  ) : (
    inner
  );
}

function FnButton({ page, onNavigate }: { page: NavPage; onNavigate: () => void }) {
  const ItemIcon = page.icon;
  const inner = (
    <>
      <span className="flex size-7 shrink-0 items-center justify-center rounded-md border border-border bg-background text-foreground">
        <ItemIcon className="size-4" />
      </span>
      <span className="min-w-0 flex-1 text-left">
        <span className="block truncate text-xs font-medium">{page.title}</span>
        <span className="block truncate text-[10px] font-normal text-muted-foreground">{page.description}</span>
      </span>
      {!page.to && <SoonTag />}
    </>
  );
  const cls = 'h-auto w-full justify-start gap-2.5 rounded-lg p-2 font-normal';
  if (page.to) {
    return (
      <Button variant="ghost" className={cls} render={<Link to={page.to} onClick={onNavigate} />}>
        {inner}
      </Button>
    );
  }
  return (
    <Button variant="ghost" disabled className={cls}>
      {inner}
    </Button>
  );
}

function BlockCard({ section, onNavigate }: { section: Section; onNavigate: () => void }) {
  const BlockIcon = section.icon;
  return (
    <Card className="gap-0 py-0" data-od-id={`launcher-block-${section.id}`}>
      <CardHeader className="flex flex-row items-center gap-2.5 p-3">
        <span className="flex size-8 shrink-0 items-center justify-center rounded-md bg-muted text-foreground">
          <BlockIcon className="size-[18px]" />
        </span>
        <div className="min-w-0 flex-1">
          <CardTitle className="text-[13px]">{section.label}</CardTitle>
          <CardDescription className="text-[11px]">{section.caption}</CardDescription>
        </div>
        {section.soon && <SoonTag />}
      </CardHeader>
      <CardContent className="grid grid-cols-1 gap-0.5 border-t p-2 sm:grid-cols-2">
        {section.pages.map((page) => (
          <FnButton key={page.title} page={page} onNavigate={onNavigate} />
        ))}
      </CardContent>
    </Card>
  );
}

/**
 * App launcher — "Центр управления". Non-modal, no panel background (cards float);
 * the sidebar stays clickable. The top region (4 hubs → 3 summary → 1 overview)
 * sits on one big backing card; the full service catalog floats below. Close
 * button sits in a thin bar above the scroll. Docked right of the sidebar.
 */
export function AppLauncher({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const close = () => onOpenChange(false);
  // Dock flush against the sidebar's right edge, following its collapsed state.
  const { state } = useSidebar();

  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange} modal={false}>
      <Dialog.Portal>
        {/* Dim everything except the sidebar (left of this) and the menu cards
            (above, z-40). Covers the header (z-10) too → menu overlaps it. */}
        <div
          aria-hidden="true"
          onClick={close}
          className={cn(
            'fixed inset-y-0 right-0 z-30 bg-black/40 duration-200 animate-in fade-in-0',
            state === 'collapsed' ? 'left-12' : 'left-64',
          )}
        />
        <Dialog.Popup
          data-od-id="launcher"
          className={cn(
            'fixed inset-y-0 z-40 flex h-full w-[min(760px,60vw)] flex-col bg-transparent outline-none transition-[left,transform,opacity] duration-200 ease-out data-ending-style:-translate-x-4 data-ending-style:opacity-0 data-starting-style:-translate-x-4 data-starting-style:opacity-0',
            state === 'collapsed' ? 'left-12' : 'left-64',
          )}
        >
          <Dialog.Title className="sr-only">Центр управления</Dialog.Title>
          <Dialog.Description className="sr-only">Все сервисы и разделы проекта</Dialog.Description>

          {/* Close bar — above the scroll, right-aligned. No title. */}
          <div className="flex h-10 shrink-0 items-center justify-end px-3">
            <Dialog.Close
              aria-label="Закрыть"
              render={
                <Button variant="ghost" size="icon-sm" className="border border-border bg-background shadow-sm" />
              }
            >
              <X className="size-4" />
            </Dialog.Close>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto px-3 pb-3">
            {/* Top region on one big backing card so it doesn't blend with the page. */}
            <Card className="mb-3 gap-2.5 p-3">
              <div className="grid grid-cols-2 gap-2.5 lg:grid-cols-4">
                {META.map((hub) => (
                  <MetaCard key={hub.name} hub={hub} onNavigate={close} />
                ))}
              </div>

              <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3">
                {SUMMARY.map((s) => (
                  <Link key={s.to} to={s.to} onClick={close} className={linkRing}>
                    <Stat
                      label={s.label}
                      value="—"
                      context="нет данных"
                      className="h-full border-0 bg-muted/60 p-3.5 transition-colors hover:bg-muted"
                    />
                  </Link>
                ))}
              </div>

              <Link to="/" onClick={close} className={linkRing}>
                <div className={cn('flex items-center gap-4 px-4 py-3.5', tile, 'hover:bg-muted')}>
                  <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-background text-foreground">
                    <SquaresFour className="size-5" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="text-sm font-medium">Обзор проекта</div>
                    <p className="text-xs text-muted-foreground">
                      Сводка ресурсов, метрики и быстрые действия — проект prod-cluster
                    </p>
                  </div>
                  <CaretRight className="size-4 shrink-0 text-muted-foreground" />
                </div>
              </Link>
            </Card>

            {/* Service catalog */}
            <div className="mb-3 text-[11px] font-medium tracking-[0.06em] text-muted-foreground uppercase">
              Все сервисы
            </div>
            <div className="grid grid-cols-1 items-start gap-3 xl:grid-cols-2">
              {SECTIONS.map((section) => (
                <BlockCard key={section.id} section={section} onNavigate={close} />
              ))}
            </div>
          </div>
        </Dialog.Popup>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
