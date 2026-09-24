import { Link } from '@tanstack/react-router';
import { Button } from '@/components/ui/button';
import { StatusPill } from '@/components/ui/status-pill';
import { bentoStatusLabel, bentoStatusVariant, requireBentoCell } from '@/components/marketing/bento/bento-data';
import { requireServiceContent } from './service-content';

/**
 * Per-service hero — copy on the left, illustration slot on the right.
 * Mirrors `marketing-hero.tsx`'s two-column shape (`3fr_2fr`, copy left,
 * visual right) but with no clone-command box: this page is narrower
 * (no second-row GitHub CTA / install one-liner — those live in the
 * landing's hero and the inverted closing CTA respectively).
 *
 * `data-service-illustration-slot={id}` on the right column is the
 * chunk-3 mount point: chunk 3 swaps the empty `<div />` for a real
 * per-service SVG illustration, picked by `id`. We deliberately render
 * nothing here rather than placeholder SVG art — chunk 3 owns that
 * asset and the brief says not to invent it.
 */
export function ServiceHero({ id }: { id: string }) {
  const cell = requireBentoCell(id);
  const content = requireServiceContent(id);

  return (
    <div className="lg:grid lg:grid-cols-[3fr_2fr] lg:items-center lg:gap-10 xl:gap-12">
      <div>
        <nav aria-label="Breadcrumb" className="flex flex-wrap items-center gap-1 text-sm text-muted-foreground">
          <Link to="/" className="transition-colors hover:text-foreground">
            Home
          </Link>
          <span aria-hidden>/</span>
          <Link to="/" hash="services" className="transition-colors hover:text-foreground">
            Services
          </Link>
          <span aria-hidden>/</span>
          <span className="text-foreground" aria-current="page">
            {cell.title}
          </span>
        </nav>

        <div className="mt-6 flex flex-wrap items-center gap-2">
          <StatusPill variant={bentoStatusVariant(cell.status)} hideDot size="sm">
            {bentoStatusLabel(cell.status)}
          </StatusPill>
        </div>

        <h1 className="mt-4 text-balance text-4xl font-extrabold tracking-tight text-foreground md:text-5xl">
          {cell.title}
        </h1>

        <p className="mt-4 max-w-2xl text-base leading-7 text-muted-foreground md:text-lg">
          {content.lead}
        </p>

        <div className="mt-8 flex flex-wrap items-center gap-3">
          <Button variant="outline" size="lg" render={<Link to={content.docsHref}>Read the docs</Link>} />
          <Button size="lg" render={<Link to="/docs/getting-started">Get started</Link>} />
        </div>
      </div>

      {/* Chunk 3 fills this in from a per-id illustration map. */}
      <div
        className="mx-auto mt-12 max-w-md lg:mt-0 lg:max-w-none lg:mx-0"
        data-service-illustration-slot={id}
      />
    </div>
  );
}