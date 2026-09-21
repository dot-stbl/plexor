import type { ReactNode } from 'react';

/**
 * Screenshot — a real screenshot from the console, lazily loaded.
 *
 * Two render shapes:
 *
 * - `<Screenshot src="…" alt="…" />` — renders a real `<img>` when the
 *   file exists. `loading="lazy"` so a page with many screens doesn't
 *   hold the browser on first paint.
 * - `<ScreenshotPlaceholder caption="…" />` — used when the screenshot
 *   has not yet been captured. The CONTENT-PLAN explicitly bans
 *   fabricated PNGs; this placeholder keeps pages presentable until
 *   a real capture lands. Caption doubles as the alt-text.
 *
 * The placeholder is the same component the file uses to render
 * `needs capture` shots. A future commit that adds a real PNG can
 * replace the placeholder with `<Screenshot>` at the same call site.
 */
export interface ScreenshotProps {
  readonly src: string;
  readonly alt: string;
  readonly caption?: string;
}

export function Screenshot({ src, alt, caption }: ScreenshotProps): ReactNode {
  return (
    <figure className="my-6">
      <div className="overflow-hidden rounded-lg border border-border bg-surface-2">
        <img
          src={src}
          alt={alt}
          loading="lazy"
          decoding="async"
          className="block h-auto w-full"
        />
      </div>
      {caption ? (
        <figcaption className="mt-2 text-center text-xs text-muted-2">
          {caption}
        </figcaption>
      ) : null}
    </figure>
  );
}

export interface ScreenshotPlaceholderProps {
  readonly caption: string;
}

export function ScreenshotPlaceholder({ caption }: ScreenshotPlaceholderProps): ReactNode {
  return (
    <figure className="my-6">
      <div
        aria-label="Screenshot pending capture"
        className="flex aspect-[16/10] items-center justify-center rounded-lg border border-dashed border-border bg-surface-2 px-6 text-center"
      >
        <div>
          <div className="font-mono text-[10px] font-medium uppercase tracking-[0.16em] text-muted-2">
            screenshot pending
          </div>
          <div className="mt-1 text-sm text-muted-foreground">{caption}</div>
        </div>
      </div>
    </figure>
  );
}
