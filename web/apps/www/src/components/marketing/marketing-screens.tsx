import { Reveal, useThemeName } from '@/components/motion';
import { EYEBROW_CLASS } from '@/components/chrome/panel';
import { requireScreenshot, type Screenshot } from '@/content/media-manifest';

const CATALOG_SHOT = requireScreenshot('catalog');
const AUDIT_SHOT = requireScreenshot('audit');

interface ScreenFigureProps {
  readonly shot: Screenshot;
  readonly label: string;
}

function ScreenFigure({ shot, label }: ScreenFigureProps) {
  const theme = useThemeName();
  const src = theme === 'dark' ? shot.dark : shot.light;

  return (
    <figure>
      <div className="mx-auto max-w-7xl overflow-hidden rounded-2xl border border-border bg-card">
        <img
          src={src}
          alt={shot.alt}
          width={shot.width}
          height={shot.height}
          loading="lazy"
          className="h-full w-full object-cover"
        />
      </div>
      <figcaption className="mt-3 text-xs text-muted-2">{label}</figcaption>
    </figure>
  );
}

/**
 * Two more screenshots of the real console — the second and third of
 * the landing's 2-3 total (the hero preview is the first). Product
 * owner feedback: screenshots only, "rarely — 2-3 for the whole thing",
 * captioned once, not per-image (`marketing-hero-preview.tsx` carries
 * the single "Real console, sample data." disclosure; these two only
 * need a short label naming what's shown). Lazy-loaded — this section
 * is the 2nd panel below the hero, so eager-loading buys nothing; the
 * hero shot in `marketing-hero-preview.tsx` stays `loading="eager"`
 * because it is the first paint.
 *
 * Stacked full-width, not a 2-up half-width grid — the console UI in
 * these shots (sidebar labels, table rows, filter fields) reads as an
 * illegible thumbnail at half the panel's width. Each figure now spans
 * the panel (capped at `max-w-7xl` = 1280px, the screenshot's native
 * captured width, so it never upscales), one below the other — the "one
 * big console shot" idea YC product pages use, twice.
 */
export function MarketingScreens() {
  return (
    <Reveal>
      <p className={EYEBROW_CLASS}>In the console</p>
      <h2 className="mt-2 max-w-2xl text-3xl font-extrabold tracking-tight text-foreground">
        A managed database, and every action logged.
      </h2>

      <div className="mt-8 flex flex-col gap-10">
        <ScreenFigure shot={CATALOG_SHOT} label="Managed PostgreSQL" />
        <ScreenFigure shot={AUDIT_SHOT} label="Audit log" />
      </div>
    </Reveal>
  );
}
