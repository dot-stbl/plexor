import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { Link } from '@tanstack/react-router';
import type { Icon } from '@nine-thirty-five/material-symbols-react';
import { useTranslation } from 'react-i18next';
import {
  Close,
  GridView,
  KeyboardArrowRight,
  MenuBook,
  OpenInNew,
  Settings,
  Tune,
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import {
  Card,
  CardDescription,
  CardTitle,
} from '@/shared/ui/primitives/card';
import { Button } from '@/shared/ui/primitives/button';
import { useSidebar } from '@/shared/ui/primitives/sidebar';
import { ScrollArea } from '@/shared/ui/primitives/scroll-area';
import { Stat } from '@/shared/ui/primitives/stat';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { cn } from '@/lib/utils';
import type { LauncherSummaryCard } from '@/mocks/launcher-summary';
import { SECTIONS, type AppRoute, type NavPage, type Section } from './nav-config';

type MetaHub = {
  nameKey: string;
  captionKey: string;
  icon: Icon;
  /** Internal route (typed). Exactly one of `to` / `href` is set per hub. */
  to?: AppRoute;
  /** External URL (opens in a new tab) — for surfaces outside the console. */
  href?: string;
  /** Not shipped yet — renders dimmed with a "soon" tag. */
  soon?: boolean;
};

/**
 * Row 1 — cross-cutting entry hubs (4). Documentation links to the external
 * docs site (web/apps/www → plexor.dev); Administration and Settings land on
 * their section's first shipped page — no `soon` tags on working targets.
 */
const META: MetaHub[] = [
  { nameKey: 'shell.overview', captionKey: 'shell.overviewCaption', icon: GridView, to: '/' },
  { nameKey: 'shell.documentation', captionKey: 'shell.documentationCaption', icon: MenuBook, href: 'https://plexor.dev/docs' },
  { nameKey: 'shell.administration', captionKey: 'shell.administrationCaption', icon: Settings, to: '/admin/branding' },
  { nameKey: 'shell.settings', captionKey: 'shell.settingsCaption', icon: Tune, to: '/settings/profile' },
];

type SummaryCard = { labelKey: string; to: AppRoute; value: string; context: string };

const linkRing = 'block rounded-lg outline-none focus-visible:ring-2 focus-visible:ring-ring/40';
// Inset tile: bg-muted so it reads against the top region's big bg-card.
const tile = 'rounded-lg bg-muted/60 transition-colors duration-150 ease-out';

const SoonTag = () => {
  const { t } = useTranslation();
  return (
    <StatusPill variant="idle" hideDot className="shrink-0 px-1.5 py-0 text-[9.5px] font-normal">
      {t('common.soon')}
    </StatusPill>
  );
};

function MetaCard({ hub, onNavigate }: { hub: MetaHub; onNavigate: () => void }) {
  const { t } = useTranslation();
  const HubIcon = hub.icon;
  const actionable = hub.to != null || hub.href != null;
  const inner = (
    <div className={cn('flex h-full items-center gap-2.5 px-3 py-2.5', tile, actionable && 'hover:bg-muted hover:-translate-y-px', !actionable && 'opacity-60')}>
      <span className="flex size-8 shrink-0 items-center justify-center rounded-md bg-background text-foreground">
        <HubIcon className="size-4" />
      </span>
      <div className="min-w-0">
        <div className="flex items-center gap-1.5">
          <span className="truncate text-xs font-medium">{t(hub.nameKey)}</span>
          {hub.href && <OpenInNew aria-hidden className="size-3 shrink-0 text-muted-foreground" />}
          {hub.soon && <SoonTag />}
        </div>
        <div className="truncate text-[10.5px] text-muted-foreground">{t(hub.captionKey)}</div>
      </div>
    </div>
  );
  if (hub.href) {
    return (
      <a href={hub.href} target="_blank" rel="noreferrer" className={linkRing}>
        {inner}
      </a>
    );
  }
  return hub.to ? (
    <Link to={hub.to} onClick={onNavigate} className={linkRing}>
      {inner}
    </Link>
  ) : (
    inner
  );
}

function FnButton({ page, onNavigate }: { page: NavPage; onNavigate: () => void }) {
  const { t } = useTranslation();
  const ItemIcon = page.icon;
  const inner = (
    <>
      <span className="flex size-7 shrink-0 items-center justify-center rounded-md border border-border bg-background text-foreground">
        <ItemIcon className="size-4" />
      </span>
      <span className="min-w-0 flex-1 text-left">
        <span className="block truncate text-xs font-medium">{t(page.title)}</span>
        <span className="block truncate text-[10px] font-normal text-muted-foreground">{t(page.description)}</span>
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
  const { t } = useTranslation();
  const BlockIcon = section.icon;
  return (
    <Card className="group/block-card gap-0 overflow-visible border-transparent py-0 transition-all duration-150 ease-out hover:-translate-y-px hover:border-border/60 hover:shadow-md" data-od-id={`launcher-block-${section.id}`}>
      <div className="flex flex-row items-center gap-2.5 border-b border-border p-3">
        <span className="flex size-8 shrink-0 items-center justify-center rounded-md bg-muted text-foreground">
          <BlockIcon className="size-[18px] transition-transform duration-200 ease-out group-hover/block-card:scale-110" />
        </span>
        <div className="min-w-0 flex-1">
          <CardTitle className="text-[13px]">{t(section.label)}</CardTitle>
          <CardDescription className="text-[11px]">{t(section.caption)}</CardDescription>
        </div>
        {section.soon && <SoonTag />}
      </div>
      <div className="grid grid-cols-1 gap-0.5 sm:grid-cols-2">
        {section.pages.map((page) => (
          <FnButton key={page.title} page={page} onNavigate={onNavigate} />
        ))}
      </div>
    </Card>
  );
}

/**
 * App launcher — "Центр управления". Non-modal, no panel background (cards float);
 * the sidebar stays clickable. The top region (4 hubs → 3 summary → 1 overview)
 * sits on one big backing card; the full service catalog floats below. Close
 * button sits inset above a custom scroll rail. Docked right of the sidebar.
 *
 * Implementation: plain React state + a portal to document.body. No base-ui
 * Dialog — we manage open/close transitions ourselves via `data-state` so we
 * stay independent of any overlay library. `useState(open)` is "what to render",
 * `rendered` is "actually in the DOM" so we can animate the close transition
 * before unmounting.
 */
export function AppLauncher({
  open,
  onOpenChange,
  summary,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Row 2's at-a-glance summary (3 cards). The composition root
   *  (`routes/__root.tsx`) fetches this via `useLauncherSummary()` and
   *  passes it down — this component never reaches into a mock/domain
   *  module itself. */
  summary: readonly LauncherSummaryCard[];
}) {
  const close = () => onOpenChange(false);
  const { t } = useTranslation();
  // Dock flush against the sidebar's right edge, following its collapsed state.
  const { state } = useSidebar();
  // Resolves the card label through `t()` so it tracks the active locale.
  const SUMMARY: SummaryCard[] = summary.map((card) => ({
    labelKey: card.labelKey,
    to: card.to as AppRoute,
    value: card.value,
    context: card.context,
  }));

  // Defer mounting so the enter animation can play; keep mounted briefly on
  // close so the exit animation can play. Mirrors base-ui's
  // `data-starting-style` / `data-ending-style` without depending on base-ui.
  const [rendered, setRendered] = useState(open);
  const [phase, setPhase] = useState<'enter' | 'idle' | 'exit'>(
    open ? 'enter' : 'idle',
  );

  useEffect(() => {
    if (open) {
      setRendered(true);
      setPhase('enter');
      // Promote to 'idle' on the next frame so the entrance transition runs.
      const id = requestAnimationFrame(() => setPhase('idle'));
      return () => cancelAnimationFrame(id);
    }
    if (rendered) {
      setPhase('exit');
      const id = window.setTimeout(() => {
        setRendered(false);
        setPhase('idle');
      }, 200);
      return () => window.clearTimeout(id);
    }
    return undefined;
  }, [open, rendered]);

  // Close on Escape.
  useEffect(() => {
    if (!rendered) return undefined;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') close();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  });

  if (!rendered || typeof document === 'undefined') return null;

  const sideOffset = state === 'collapsed' ? 'left-12' : 'left-64';

  return createPortal(
    <div
      data-od-id="launcher-portal"
      data-state={phase === 'exit' ? 'closed' : 'open'}
      // pointer-events-none so the sidebar underneath stays clickable.
      // Children that need clicks (dim overlay below, launcher panel as a
      // sibling inside this portal) re-enable pointer-events explicitly
      // because pointer-events doesn't inherit 'auto' from 'none'.
      className="pointer-events-none fixed inset-0 z-fixed"
    >
      {/* Dim everything except the sidebar (left of this) and the menu cards.
          Covers the header (z-sticky) too → menu overlaps it. */}
      <div
        aria-hidden="true"
        onClick={close}
        className={cn(
          'pointer-events-auto fixed inset-y-0 right-0 z-sticky bg-black/40 backdrop-blur-sm transition-opacity duration-200',
          sideOffset,
          phase === 'exit' ? 'opacity-0' : 'opacity-100',
        )}
      />
      <div
        data-od-id="launcher"
        className={cn(
          'pointer-events-auto fixed inset-y-0 z-fixed flex h-full w-[min(760px,60vw)] flex-col bg-transparent outline-none transition-[transform,opacity] duration-200 ease-out',
          sideOffset,
          phase === 'enter' && '-translate-x-4 opacity-0',
          phase === 'exit' && '-translate-x-4 opacity-0',
        )}
      >
        <h2 className="sr-only">{t('shell.launcher.heading')}</h2>
        <p className="sr-only">{t('shell.launcher.description')}</p>

        <div className="flex min-h-0 flex-1">
          <ScrollArea
            variant="themed"
            className="min-h-0 flex-1"
            viewportClassName="pr-1.5"
          >
            <div className="p-3.5 pr-0">
              {/* Top region on one big backing card. */}
              <Card className="mb-3 gap-2.5 p-3.5">
                <div className="grid grid-cols-2 gap-2.5 lg:grid-cols-4">
                  {META.map((hub, index) => (
                    <div
                      key={hub.nameKey}
                      className="animate-in fade-in slide-in-from-top-2 fill-mode-both duration-200"
                      style={{ animationDelay: `${Math.min(index, 6) * 40}ms` }}
                    >
                      <MetaCard hub={hub} onNavigate={close} />
                    </div>
                  ))}
                </div>

                <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-3">
                  {SUMMARY.map((s, index) => (
                    <Link
                      key={s.to}
                      to={s.to}
                      onClick={close}
                      className={cn(
                        linkRing,
                        'animate-in fade-in slide-in-from-top-2 fill-mode-both duration-200',
                      )}
                      style={{ animationDelay: `${160 + Math.min(index, 4) * 40}ms` }}
                    >
                      <Stat
                        label={t(s.labelKey)}
                        value={s.value}
                        context={s.context}
                        className="h-full border-0 bg-muted/60 p-3.5 transition-all duration-150 ease-out hover:-translate-y-px hover:bg-muted hover:shadow-sm"
                      />
                    </Link>
                  ))}
                </div>

                <Link to="/" onClick={close} className={linkRing} data-od-id="launcher-overview-row">
                  <div className={cn('group/overview flex items-center gap-4 px-4 py-3.5', tile, 'hover:bg-muted')}>
                    <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-background text-foreground">
                      <GridView className="size-5 transition-transform duration-200 ease-out group-hover/overview:scale-110" />
                    </span>
                    <div className="min-w-0 flex-1">
                      <div className="text-sm font-medium">{t('shell.launcher.overview.title')}</div>
                      <p className="text-xs text-muted-foreground">
                        {t('shell.launcher.overview.description')}
                      </p>
                    </div>
                    <KeyboardArrowRight className="size-4 shrink-0 text-muted-foreground transition-all duration-200 ease-out group-hover/overview:translate-x-0.5 group-hover/overview:text-foreground" />
                  </div>
                </Link>
              </Card>

              {/* Service catalog */}
              <div className="grid grid-cols-1 items-start gap-3 xl:grid-cols-2">
                {SECTIONS.map((section, index) => (
                  <div
                    key={section.id}
                    className="animate-in fade-in slide-in-from-bottom-2 fill-mode-both duration-200"
                    style={{ animationDelay: `${280 + Math.min(index, 8) * 30}ms` }}
                  >
                    <BlockCard section={section} onNavigate={close} />
                  </div>
                ))}
              </div>
            </div>
          </ScrollArea>

          {/* Right-edge breathing room — just enough space for the Close so it
              sits visually separated from the cards without looking like
              a separate panel or side header. */}
          <div
            data-od-id="launcher-rail"
            className="flex w-10 shrink-0 items-start justify-center pt-2"
          >
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label={t('common.close')}
              onClick={close}
              className="size-7 rounded-md text-muted-foreground hover:text-foreground"
            >
              <Close className="size-4 transition-transform duration-200 ease-out hover:rotate-90" />
            </Button>
          </div>
        </div>
      </div>
    </div>,
    document.body,
  );
}
