import type { ReactNode } from 'react';
import { ScreenshotPlaceholder } from './screenshot';

/**
 * Diagram — light wrapper around the inline-SVG diagrams used in the
 * concepts chapter (the five diagrams D1–D5 from the CONTENT-PLAN).
 *
 * In production the SVG lives in `public/diagrams/<id>.svg` and the
 * component renders an `<img>` referencing it with the same visual
 * treatment as a screenshot.
 *
 * Until the SVG is authored, callers use `<DiagramPlaceholder id="D1"
 * caption="Org → Team → Folder tree" />` — the placeholder matches
 * `ScreenshotPlaceholder` so authors can drop them in at the same
 * call sites in the prose.
 */
export interface DiagramProps {
  readonly src: string;
  readonly id: string;
  readonly caption: string;
  readonly alt: string;
}

export function Diagram({ src, id, caption, alt }: DiagramProps): ReactNode {
  return (
    <figure className="my-6">
      <div className="overflow-hidden rounded-lg border border-border bg-surface-2">
        <img
          src={src}
          alt={alt}
          loading="lazy"
          decoding="async"
          data-diagram={id}
          className="block h-auto w-full"
        />
      </div>
      <figcaption className="mt-2 text-center text-xs text-muted-2">
        <span className="font-mono text-[10px] uppercase tracking-[0.14em] text-muted-2/80">
          Diagram {id}
        </span>{' '}
        · {caption}
      </figcaption>
    </figure>
  );
}

export interface DiagramPlaceholderProps {
  readonly id: string;
  readonly caption: string;
}

export function DiagramPlaceholder({ id, caption }: DiagramPlaceholderProps): ReactNode {
  return (
    <ScreenshotPlaceholder
      caption={`Diagram ${id} · ${caption}`}
    />
  );
}
