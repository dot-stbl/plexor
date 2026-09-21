'use client';

/**
 * Editorial section divider for the Plexor docs sidebar. Replaces the
 * default fumadocs separator with a numbered eyebrow (`01`, `02`, ...)
 * followed by the title (uppercase, 0.06em tracking) and a filling
 * rule that runs to the right edge of the sidebar.
 *
 * The mono-spaced number sits muted at 60% opacity so it reads as
 * metadata, not as content. The `.docs-chapter-num` and
 * `.docs-chapter-rule` class names are targeted by the
 * `prefers-reduced-motion` rule in `app/global.css` so the divider
 * respects the user's motion preference.
 */
export function ChapterLabel({
  index,
  title,
}: {
  index: number;
  title: React.ReactNode;
}) {
  const num = String(index + 1).padStart(2, '0');
  return (
    <div className="px-2 pt-5 pb-1.5 flex items-center gap-2">
      <span className="docs-chapter-num font-mono text-[10px] tracking-[0.12em] text-fd-muted-foreground/60">
        {num}
      </span>
      <span className="text-[11px] font-medium tracking-[0.06em] uppercase text-fd-muted-foreground">
        {title}
      </span>
      <span className="docs-chapter-rule flex-1 border-t border-fd-border/60" />
    </div>
  );
}

/**
 * Tracks the index of the current Separator render so we can stamp
 * chapter numbers on them. Module-scoped because the page tree
 * renders all separators in one pass within a single render, and
 * the counter resets when the pathname changes (locale switch).
 *
 * Side effects during render: the increment is a write during render
 * — React's strict mode will render the component twice in dev, so
 * the counter would briefly double before the next render settles
 * it. For a non-essential display counter this is the established
 * pattern ("You Might Not Need an Effect" — derived values like
 * this are idiomatic with refs during render).
 */
const chapterState = { counter: 0, pathname: '' };

export function useChapterIndex(): number {
  const pathname = usePathname();
  if (chapterState.pathname !== pathname) {
    chapterState.counter = 0;
    chapterState.pathname = pathname;
  }
  const current = chapterState.counter;
  chapterState.counter = current + 1;
  return current;
}

// Late import: avoids a top-of-file cycle with this module's JSX.
import { usePathname } from 'fumadocs-core/framework';