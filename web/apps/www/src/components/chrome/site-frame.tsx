import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

/**
 * Shared full-width frame class — header, footer, `SiteFrame` and the
 * docs 3-column grid all align to the same left/right edge. Full width
 * up to a very wide cap (`max-w-[1760px]`) so the page uses the
 * viewport instead of sitting in a centered `max-w-6xl` column (product
 * owner feedback: "expand content to the full width instead of a block
 * only in the middle"). `xl:px-16` gives the gutter room to breathe once
 * the cap itself is wide enough that `px-10` alone reads cramped.
 */
export const FRAME_CLASS = 'mx-auto w-full max-w-[1760px] px-6 md:px-10 xl:px-16';

/**
 * Page frame — mount once per page, around every `<FrameSection>`. No
 * rails, no corner marks: those were removed (product owner feedback,
 * see `FRAME_CLASS`) in favor of a plain full-width container. Sections
 * separate with a `border-t` hairline (`<FrameSection>`); `SiteFrame`
 * itself contributes no border.
 */
export function SiteFrame({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn('w-full', className)}>{children}</div>;
}

interface FrameSectionProps {
  readonly children: ReactNode;
  readonly id?: string;
  readonly className?: string;
  /** Opt out of the default `px-6 md:px-10 xl:px-16 py-16 md:py-24` padding. */
  readonly bleed?: boolean;
}

/**
 * One border-t hairline band with the shared padding rhythm. `bleed`
 * opts a section out of the default padding (e.g. a section that wants
 * to manage its own inner spacing) but still gets the `border-t` +
 * `FRAME_CLASS` width — content inside a `bleed` section applies
 * `FRAME_CLASS` itself where it needs the aligned gutter.
 */
export function FrameSection({ children, id, className, bleed = false }: FrameSectionProps) {
  return (
    <section id={id} className="relative w-full border-t border-border">
      <div className={cn(FRAME_CLASS, !bleed && 'py-16 md:py-24', className)}>{children}</div>
    </section>
  );
}
