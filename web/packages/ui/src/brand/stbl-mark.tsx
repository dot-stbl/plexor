import type { ImgHTMLAttributes } from 'react';

/**
 * STBL organization mark — the official `.stbl` logo from
 * `raw.githubusercontent.com/dot-stbl/.github/main/assets/logo.svg`,
 * vendored locally as `public/stbl-logo.svg` so the chrome doesn't depend
 * on github.com being reachable (offline dev, air-gapped deploys, slow CDN).
 *
 * Two squares: outer (fg) and inner (bg). The SVG inlines a
 * `prefers-color-scheme: dark` media query that swaps fg/bg so the mark
 * stays visible against the app shell's background-foreground flip.
 *
 * The favicon ships a separate, larger SVG (favicon.svg) because
 * browser tabs prefer a fully self-contained inline file — favicons
 * can't go through Vite's asset pipeline.
 */
export function StblMark({ className, ...rest }: ImgHTMLAttributes<HTMLImageElement>) {
  return (
    <img
      src="/stbl-logo.svg"
      alt=""
      aria-hidden="true"
      className={['inline-block size-4 shrink-0', className].filter(Boolean).join(' ')}
      {...rest}
    />
  );
}