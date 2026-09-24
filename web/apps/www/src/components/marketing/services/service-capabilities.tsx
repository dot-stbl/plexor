import { StatusPill } from '@/components/ui/status-pill';
import { EYEBROW_CLASS } from '@/components/chrome/panel';
import { requireBentoCell } from '@/components/marketing/bento/bento-data';
import { requireServiceContent, type ServiceCapability } from './service-content';

interface CapabilityCardProps {
  readonly capability: ServiceCapability;
}

/**
 * Single capability card — mirrors `BentoCell`'s icon-title-body rhythm but
 * without the icon (capabilities are listed copy, not a hex of features)
 * and with a `Planned` badge instead of a shipped/next badge. The page-
 * level hero pill already says "Shipped" or "Next" once for the whole
 * page — per-card badges would be redundant noise.
 */
function CapabilityCard({ capability }: CapabilityCardProps) {
  return (
    <div className="relative h-full rounded-2xl bg-card p-6">
      {capability.planned && (
        <div className="absolute right-4 top-4">
          <StatusPill variant="warn" hideDot size="sm">
            Planned
          </StatusPill>
        </div>
      )}
      <h3 className="text-base font-semibold text-foreground">{capability.title}</h3>
      <p className="mt-1.5 text-sm leading-6 text-muted-foreground">{capability.body}</p>
    </div>
  );
}

/**
 * Capabilities section — eyebrow + heading + 3-col grid. Caller wraps this
 * in a `Panel` (see `service-page.tsx`) so the section inherits the page's
 * panel fill rhythm without each component owning chrome.
 *
 * Heading copy is honest about what's shipped vs planned (driven by
 * `cell.status`, not by `capability.planned` — a single source of truth
 * per page rather than inferring the headline from a single capability's
 * flag).
 */
export function ServiceCapabilities({ id }: { id: string }) {
  const cell = requireBentoCell(id);
  const content = requireServiceContent(id);
  const heading = cell.status === 'shipped' ? "What ships today." : "What's planned.";

  return (
    <div>
      <p className={EYEBROW_CLASS}>Capabilities</p>
      <h2 className="mt-2 max-w-2xl text-3xl font-extrabold tracking-tight text-foreground">
        {heading}
      </h2>

      <div className="mt-8 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 md:gap-6">
        {content.capabilities.map((capability) => (
          <CapabilityCard key={capability.title} capability={capability} />
        ))}
      </div>
    </div>
  );
}