import { useThemeName } from '@/components/motion';
import { EYEBROW_CLASS } from '@/components/chrome/panel';
import { requireScreenshot } from '@/content/media-manifest';
import { requireServiceContent } from './service-content';

/**
 * One theme-aware console screenshot per service page that has one
 * (`compute`, `quotas-audit`, `app-catalog`). `marketing-hero-preview.tsx`
 * does the same shape on the landing's hero; this component is the
 * per-page equivalent, captioned with the page's own copy from
 * `SERVICE_CONTENT.screenshot.caption` rather than the generic "Real
 * console, sample data." disclosure.
 *
 * Returns `null` when the service has no `screenshot` in
 * `SERVICE_CONTENT` (e.g. `networking`, `storage`, `identity`). The
 * composition root (`service-page.tsx`) only mounts this component
 * conditionally — the `null` return is a belt-and-braces fallback for
 * any future caller that forgets the guard.
 */
export function ServiceConsolePanel({ id }: { id: string }) {
  const theme = useThemeName();
  const content = requireServiceContent(id);
  const screenshot = content.screenshot;
  if (!screenshot) {
    return null;
  }

  const shot = requireScreenshot(screenshot.id);
  const src = theme === 'dark' ? shot.dark : shot.light;

  return (
    <figure>
      <p className={EYEBROW_CLASS}>In the console</p>
      <div className="mx-auto mt-6 max-w-7xl overflow-hidden rounded-xl border border-border bg-card">
        <img
          src={src}
          alt={shot.alt}
          width={shot.width}
          height={shot.height}
          loading="lazy"
          className="h-full w-full object-cover"
        />
      </div>
      <figcaption className="mt-3 text-xs text-muted-2">{screenshot.caption}</figcaption>
    </figure>
  );
}