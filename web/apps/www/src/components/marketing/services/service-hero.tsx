import { Link } from '@tanstack/react-router';
import { Button } from '@/components/ui/button';
import { StatusPill } from '@/components/ui/status-pill';
import { bentoStatusLabel, bentoStatusVariant, requireBentoCell } from '@/components/marketing/bento/bento-data';
import { requireServiceContent } from './service-content';
import { requireServiceIllustration } from './service-illustrations';

/**
 * Per-service hero — copy on the left, illustration on the right.
 * Mirrors `marketing-hero.tsx`'s two-column shape (`3fr_2fr`, copy left,
 * visual right) but with no clone-command box: this page is narrower
 * (no second-row GitHub CTA / install one-liner — those live in the
 * landing's hero and the inverted closing CTA respectively).
 *
 * The right column renders the per-id SVG illustration from
 * `service-illustrations.tsx`; the wrapping `data-service-illustration-slot`
 * attribute stays for visual-test selectors that target the slot, not the
 * inner svg.
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

      <div
        className="mx-auto mt-12 max-w-md lg:mt-0 lg:max-w-none lg:mx-0"
        data-service-illustration-slot={id}
      >
        {requireServiceIllustration(id)()}
      </div>
    </div>
  );
}