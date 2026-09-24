import { Link } from '@tanstack/react-router';
import { EYEBROW_CLASS } from '@/components/chrome/panel';
import { BENTO_CELLS } from '@/components/marketing/bento/bento-data';
import { requireServiceContent } from './service-content';

/**
 * Two related-link blocks at the bottom of a service page — same Panel
 * (caller-wrapped):
 *
 *   1. Related docs — the curated doc links the service author pointed at
 *      (`SERVICE_CONTENT.relatedDocs`). Reads as plain text links with a
 *      trailing arrow, not buttons; this is a discovery surface, not a
 *      primary CTA.
 *   2. Other services — every OTHER bento cell, as a row of icon+title
 *      chips linking to `/services/<id>`. The current page is filtered
 *      out so a user never sees a chip that links back to the page
 *      they're already on.
 *
 * Chip styling mirrors the `VisualChipRow` decorative chips in
 * `bento-visuals.tsx` (same `rounded-full border border-border` shape),
 * sized up to `px-3 py-1.5 text-sm` because these chips are interactive
 * and need readable touch targets, not the `text-[10px]` decorative size.
 */
export function ServiceRelated({ id }: { id: string }) {
  const content = requireServiceContent(id);
  const otherServices = BENTO_CELLS.filter((cell) => cell.id !== id);

  return (
    <div>
      <div>
        <p className={EYEBROW_CLASS}>Related docs</p>
        <ul className="mt-4 grid grid-cols-1 gap-2 sm:grid-cols-2">
          {content.relatedDocs.map((doc) => (
            <li key={doc.to}>
              <Link
                to={doc.to}
                className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
              >
                <span>{doc.label}</span>
                <span aria-hidden>→</span>
              </Link>
            </li>
          ))}
        </ul>
      </div>

      <div className="mt-10">
        <p className={EYEBROW_CLASS}>Other services</p>
        <ul className="mt-4 flex flex-wrap gap-2">
          {otherServices.map((cell) => {
            const CellIcon = cell.icon;
            const otherServiceHref: string = `/services/${cell.id}`;
            return (
              <li key={cell.id}>
                <Link
                  to={otherServiceHref}
                  className="inline-flex items-center gap-2 rounded-full border border-border bg-background px-3 py-1.5 text-sm text-foreground transition-colors hover:bg-muted"
                >
                  <CellIcon className="size-4 text-muted-foreground" aria-hidden />
                  <span>{cell.title}</span>
                </Link>
              </li>
            );
          })}
        </ul>
      </div>
    </div>
  );
}