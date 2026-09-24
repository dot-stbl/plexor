import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

/**
 * Panel system — the YC-informed page grid (product owner decision,
 * 2026-09-24 landing restyle): the marketing landing, `/changelog` and
 * the `/docs` entry page are a stack of big FLAT rounded panels (solid
 * token fills, no borders, no shadows) inside a contained column, with
 * small gaps between panels showing the page background through the
 * seams — not the old full-width `border-t` hairline rhythm
 * (`FrameSection` in `site-frame.tsx`, still used as-is by the header,
 * footer and docs 3-column grid — this file does not touch those).
 *
 * `PanelContainer` establishes the contained width ONCE per page —
 * ~1232px content at a 1440px viewport (104px gutter each side),
 * scaling up to a ~1440px content cap at 1920px and wider (the outer
 * element caps at `max-w-[1648px]`, so 1648 - 104*2 = 1440). Individual
 * `Panel`s are plain chrome divs (rounded-3xl + fill + padding) meant to
 * be stacked or paired with plain flex/grid inside one `PanelContainer`
 * — they do NOT re-apply the contained width themselves, so two `Panel`s
 * can sit side by side in a `grid lg:grid-cols-2` row and still add up
 * to the same contained width as a full-bleed single panel.
 *
 * Fill tokens (monochrome only — no brand color, no gradients):
 *   - `card`     bg-card     — brightest flat tone; hero + primary panels.
 *   - `muted`    bg-muted    — soft gray; secondary content panels.
 *   - `sunken`   bg-surface-3 — one step deeper gray; alternation only,
 *                so two adjacent panels never share the same tone.
 *   - `inverted` bg-foreground text-background — the one high-contrast
 *                "black block" beat per page (YC's black panels),
 *                achieved with existing theme-aware tokens, no new color.
 *
 * `EYEBROW_CLASS` replaces the old `font-mono uppercase tracking-[0.16em]`
 * eyebrow on every restyled section — plain sentence-case per the
 * product owner's decision (drop the mono/uppercase treatment on the
 * landing only; docs/console keep their own conventions untouched).
 */
export const PANEL_CONTAINER_CLASS = 'mx-auto w-full max-w-[1648px] px-6 md:px-10 lg:px-[104px]';

export type PanelFill = 'card' | 'muted' | 'sunken' | 'inverted';

const FILL_CLASSES: Readonly<Record<PanelFill, string>> = {
  card: 'bg-card text-card-foreground',
  muted: 'bg-muted text-foreground',
  sunken: 'bg-surface-3 text-foreground',
  inverted: 'bg-foreground text-background',
};

export const EYEBROW_CLASS = 'text-sm font-medium text-muted-foreground';

/**
 * Button recipes for content sitting on an `inverted` `Panel`. The
 * `Button` primitive's `default`/`outline` variants are tuned for a
 * `bg-background`/`bg-card` page — on an inverted panel (`bg-foreground`)
 * they read as near-invisible (in light mode `--accent` and
 * `--foreground` are both near-black; in dark mode `border-border` is
 * calibrated for a dark panel, not the light one `inverted` becomes).
 * These two recipes flip to the page's own background/foreground pair —
 * always the correct contrasting direction in both themes, using only
 * existing tokens (no new colors): merge onto `<Button className={...}>`.
 */
export const INVERTED_BUTTON_FILLED_CLASS = 'bg-background text-foreground hover:bg-background/90';
export const INVERTED_BUTTON_OUTLINE_CLASS = 'border-background/30 text-background hover:bg-background/10';

interface PanelContainerProps {
  readonly children: ReactNode;
  readonly className?: string;
}

/** Mount once per page, directly inside `SiteFrame` — see file header. */
export function PanelContainer({ children, className }: PanelContainerProps) {
  return <div className={cn(PANEL_CONTAINER_CLASS, className)}>{children}</div>;
}

interface PanelProps {
  readonly children: ReactNode;
  readonly fill?: PanelFill;
  readonly className?: string;
  readonly id?: string;
}

/** One flat rounded panel — no border, no shadow. Stack or grid several inside one `PanelContainer`. */
export function Panel({ children, fill = 'card', className, id }: PanelProps) {
  return (
    <div id={id} className={cn('rounded-3xl p-8 md:p-12 lg:p-16', FILL_CLASSES[fill], className)}>
      {children}
    </div>
  );
}

/** Vertical rhythm for a page's panel stack — small gaps, page bg shows through. */
export function PanelStack({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn('flex flex-col gap-4 py-6 md:gap-6 md:py-10', className)}>{children}</div>;
}

/** A 2-up row of panels at `lg:` (e.g. the "how it runs" light/inverted pair) — stacked below that. */
export function PanelRow({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn('grid grid-cols-1 gap-4 md:gap-6 lg:grid-cols-2', className)}>{children}</div>;
}
