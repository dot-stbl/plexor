import { Stagger, StaggerItem } from '@/components/motion';
import { EYEBROW_CLASS } from '@/components/chrome/panel';
import { cn } from '@/lib/utils';
import { BentoCell } from './bento/bento-cell';
import { BENTO_CELLS } from './bento/bento-data';

/**
 * Bento grid — six individually-rounded cards in a real spaced grid
 * (no more `gap-px bg-border` hairline trick from the old full-width
 * `FrameSection` rhythm). The wrapping `Panel` (see
 * `routes/(marketing)/index.tsx`) supplies the page-level outer
 * padding, so this component only owns the gap between heading and
 * grid. Cells stagger in as the grid enters view; each cell's own
 * mini visual then goes live independently (see
 * `bento/bento-visuals.tsx`) once *that* cell crosses the viewport —
 * `Stagger` here only owns the entrance, not the visuals' timing.
 *
 * 3 columns up to `lg`, 4 on `xl` — the last two cells (both "next",
 * not yet shipped) span 2 columns each on `xl` so a 6-cell grid fills a
 * 4-column row evenly instead of leaving a half-empty row.
 */
export function MarketingFeatures() {
  return (
    <div>
      <div className="mb-8">
        <p className={EYEBROW_CLASS}>Services</p>
        <h2 className="mt-2 max-w-2xl text-3xl font-extrabold tracking-tight text-foreground">
          Six systems, one binary.
        </h2>
      </div>

      <Stagger className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 md:gap-6">
        {BENTO_CELLS.map((cell, index) => (
          // `h-full`: the grid item itself already stretches to the row's
          // height (CSS Grid's default `align-items: stretch`) — this
          // just passes that height down to `BentoCell`'s own `h-full`
          // so its `bg-card` fill reaches every edge, same as before the
          // `StaggerItem` wrapper was introduced.
          <StaggerItem key={cell.id} className={cn('h-full', index >= 4 && 'xl:col-span-2')}>
            <BentoCell cell={cell} />
          </StaggerItem>
        ))}
      </Stagger>
    </div>
  );
}
