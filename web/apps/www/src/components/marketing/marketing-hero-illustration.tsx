import { cn } from '@/lib/utils';

interface MarketingHeroIllustrationProps {
  readonly className?: string;
}

/** 4x3 vent-perforation grid punched into the chassis (fill-border dots). */
const VENT_DOTS: readonly (readonly [number, number])[] = [
  [76, 210],
  [92, 210],
  [108, 210],
  [124, 210],
  [76, 226],
  [92, 226],
  [108, 226],
  [124, 226],
  [76, 242],
  [92, 242],
  [108, 242],
  [124, 242],
];

/**
 * Decorative hero illustration — a self-hosted server/rack composition
 * in the YC "big bold flat cut-out" spirit: two large overlapping solid
 * shapes at confident scale, monochrome via Plexor DS tokens only (no
 * hex, no gradients, no shadows, no decorative strokes). `aria-hidden`,
 * viewBox-based, no raster.
 *
 * Composition (viewBox 400x400):
 *   - a big rounded chassis (`fill-muted`) with 3 drive-bay slots
 *     (`fill-card`, reads as recessed cutouts against the panel behind
 *     it) and a 4x3 vent-perforation dot grid (`fill-border`)
 *   - a solid ink rack shape (`fill-foreground`) overlapping the
 *     chassis's right/bottom edge, notched at the corner where the two
 *     shapes meet, bleeding past the viewBox's right edge (clipped by
 *     the svg root's own default `overflow: hidden` — no change to the
 *     shared `Panel` chrome needed for the crop)
 *   - one pill-shaped switch/connector (`fill-muted-foreground` +
 *     `fill-card` knob) bridging the seam between the two shapes
 *   - 3 status LEDs — the only color in the piece: `fill-ok`,
 *     `fill-warn`, and one muted/idle dot
 *
 * A very subtle whole-piece hover scale is the only motion, and it's a
 * no-op under `prefers-reduced-motion` via Tailwind's `motion-reduce:`
 * variant (no JS/hook needed for a static transform).
 */
export function MarketingHeroIllustration({ className }: MarketingHeroIllustrationProps) {
  return (
    <div
      className={cn(
        'mx-auto aspect-square w-full max-w-xs transition-transform duration-300 hover:scale-[1.015] motion-reduce:transition-none motion-reduce:hover:scale-100 lg:mx-0 lg:max-w-none',
        className,
      )}
    >
      <svg
        viewBox="0 0 400 400"
        fill="none"
        role="img"
        aria-hidden="true"
        className="block h-full w-full overflow-hidden"
      >
        <rect x="48" y="64" width="240" height="240" rx="28" className="fill-muted" />

        <rect x="76" y="104" width="96" height="18" rx="9" className="fill-card" />
        <rect x="76" y="138" width="96" height="18" rx="9" className="fill-card" />
        <rect x="76" y="172" width="96" height="18" rx="9" className="fill-card" />

        {VENT_DOTS.map(([cx, cy]) => (
          <circle key={`vent-${cx}-${cy}`} cx={cx} cy={cy} r="3" className="fill-border" />
        ))}

        <path
          d="M216,240 L216,344 Q216,372 244,372 L396,372 Q424,372 424,344 L424,204 Q424,176 396,176 L280,176 L280,240 Z"
          className="fill-foreground"
        />

        <rect x="150" y="256" width="100" height="34" rx="17" className="fill-muted-foreground" />
        <circle cx="228" cy="273" r="11" className="fill-card" />

        <circle cx="256" cy="92" r="9" className="fill-ok" />
        <circle cx="356" cy="232" r="9" className="fill-warn" />
        <circle cx="376" cy="320" r="6" className="fill-muted-foreground" />
      </svg>
    </div>
  );
}
