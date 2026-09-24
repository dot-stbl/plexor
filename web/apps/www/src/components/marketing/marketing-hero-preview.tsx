import { useThemeName } from '@/components/motion';
import { requireScreenshot } from '@/content/media-manifest';

/**
 * Hero product shot — a single static screenshot of the real console
 * (mock data), theme-aware via `useThemeName()`. Replaces the earlier
 * looping video: the product owner asked for screenshots only, at most
 * 2-3 across the whole landing (this is the first; see
 * `marketing-screens.tsx` for the other two). Eager + high fetch
 * priority since this is the largest above-the-fold image on the page
 * (the other two also load eagerly — all 3 sit within the first
 * couple of screens' worth of scroll, so there's no real "below the
 * fold" win to lazy-load for).
 *
 * Deliberately no `decoding="async"`: verified (this app's own
 * `bun run shot ... --full` visual-check tool) that it races Chromium's
 * full-page screenshot capture — the image's network fetch completes
 * (so it isn't reported as a broken image) but the bitmap decode can
 * still be mid-flight when the frame is captured, painting a blank
 * `bg-card` box. The default (browser-choice) decoding avoids that.
 *
 * Capped at `max-w-7xl` (1280px) — the screenshot's native captured
 * width (`scripts/capture/lib/screenshot.ts`) — so a very wide
 * viewport doesn't upscale it past its real resolution.
 */
const HERO_SHOT = requireScreenshot('hero');

export function MarketingHeroPreview() {
  const theme = useThemeName();
  const src = theme === 'dark' ? HERO_SHOT.dark : HERO_SHOT.light;

  return (
    <div>
      <div className="max-w-7xl overflow-hidden rounded-xl border border-border bg-card">
        <img
          src={src}
          alt={HERO_SHOT.alt}
          width={HERO_SHOT.width}
          height={HERO_SHOT.height}
          loading="eager"
          fetchPriority="high"
          className="h-full w-full object-cover"
        />
      </div>
      <p className="mt-3 font-mono text-xs text-muted-2">Real console, sample data.</p>
    </div>
  );
}
