import type { ReactNode } from 'react';

/**
 * Per-service hero illustrations (chunk 3 of the services pages).
 *
 * Six small flat geometric SVG compositions in the same visual vocabulary as
 * the landing hero (`marketing-hero-illustration.tsx`): rounded `<rect>`s +
 * `<circle>`s + one `<path>` (the identity shield), Plexor DS token fills
 * only, `aria-hidden`, `viewBox 300x300`, the same aspect-square wrapper +
 * subtle hover scale, and the same status-LED motif (1–2 small
 * `fill-ok`/`fill-warn`/`fill-muted-foreground` dots per piece). Networking
 * is the one illustration allowed a `stroke-border` connector line — a
 * pure-fill set of nodes can't visually read as a graph.
 *
 * Each illustration is one tiny component below; `SERVICE_ILLUSTRATIONS`
 * is the per-id lookup used by `service-hero.tsx` to pick the right one
 * for the current page.
 */

function IllustrationFrame({ children }: { readonly children: ReactNode }) {
  return (
    <div className="mx-auto aspect-square w-full max-w-xs transition-transform duration-300 hover:scale-[1.015] motion-reduce:transition-none motion-reduce:hover:scale-100 lg:mx-0 lg:max-w-none">
      <svg
        viewBox="0 0 300 300"
        fill="none"
        role="img"
        aria-hidden="true"
        className="block h-full w-full overflow-hidden"
      >
        {children}
      </svg>
    </div>
  );
}

/** Compute — two stacked rack units, each with two drive-slot bars + one status LED. */
function ComputeIllustration() {
  return (
    <IllustrationFrame>
      <rect x="54" y="72" width="192" height="72" rx="14" className="fill-muted" />
      <rect x="72" y="92" width="108" height="10" rx="5" className="fill-card" />
      <rect x="72" y="112" width="108" height="10" rx="5" className="fill-card" />
      <circle cx="220" cy="108" r="6" className="fill-ok" />

      <rect x="54" y="156" width="192" height="72" rx="14" className="fill-muted" />
      <rect x="72" y="176" width="108" height="10" rx="5" className="fill-card" />
      <rect x="72" y="196" width="108" height="10" rx="5" className="fill-card" />
    </IllustrationFrame>
  );
}

/** Networking — a central hub + three satellite nodes joined by thin connector lines. */
function NetworkingIllustration() {
  return (
    <IllustrationFrame>
      <line
        x1="80"
        y1="88"
        x2="150"
        y2="150"
        className="stroke-border"
        fill="none"
        strokeWidth={2}
      />
      <line
        x1="220"
        y1="88"
        x2="150"
        y2="150"
        className="stroke-border"
        fill="none"
        strokeWidth={2}
      />
      <line
        x1="150"
        y1="222"
        x2="150"
        y2="150"
        className="stroke-border"
        fill="none"
        strokeWidth={2}
      />

      <circle cx="150" cy="150" r="20" className="fill-foreground" />
      <circle cx="80" cy="88" r="14" className="fill-muted" />
      <circle cx="220" cy="88" r="14" className="fill-ok" />
      <circle cx="150" cy="222" r="14" className="fill-muted" />
    </IllustrationFrame>
  );
}

/** Storage — three stacked drives, the top one highlighted as active, with a warn capacity LED. */
function StorageIllustration() {
  return (
    <IllustrationFrame>
      <rect x="64" y="232" width="172" height="24" rx="12" className="fill-muted" />
      <rect x="64" y="204" width="172" height="24" rx="12" className="fill-muted" />
      <rect x="64" y="176" width="172" height="24" rx="12" className="fill-foreground" />
      <circle cx="246" cy="148" r="7" className="fill-warn" />
    </IllustrationFrame>
  );
}

/** Identity — shield silhouette with a card-coloured keyhole dot + ok LED. */
function IdentityIllustration() {
  return (
    <IllustrationFrame>
      <path
        d="M 80,82 Q 80,64 98,64 L 202,64 Q 220,64 220,82 L 220,178 Q 220,216 150,254 Q 80,216 80,178 Z"
        className="fill-muted"
      />
      <circle cx="150" cy="146" r="14" className="fill-card" />
      <circle cx="222" cy="92" r="6" className="fill-ok" />
    </IllustrationFrame>
  );
}

/** Quotas & audit — capacity bar with a warn LED + three audit-log-row bars. */
function QuotasAuditIllustration() {
  return (
    <IllustrationFrame>
      <rect x="56" y="80" width="180" height="18" rx="9" className="fill-muted" />
      <rect x="56" y="80" width="138" height="18" rx="9" className="fill-foreground" />
      <circle cx="252" cy="89" r="7" className="fill-warn" />

      <rect x="56" y="140" width="196" height="10" rx="5" className="fill-muted-foreground" />
      <rect x="56" y="164" width="170" height="10" rx="5" className="fill-muted-foreground" />
      <rect x="56" y="188" width="136" height="10" rx="5" className="fill-muted-foreground" />
    </IllustrationFrame>
  );
}

/** App catalog — 2×3 grid of install tiles, one highlighted as selected. */
function AppCatalogIllustration() {
  return (
    <IllustrationFrame>
      <rect x="72" y="60" width="72" height="52" rx="10" className="fill-muted" />
      <rect x="156" y="60" width="72" height="52" rx="10" className="fill-foreground" />
      <rect x="72" y="124" width="72" height="52" rx="10" className="fill-muted" />
      <rect x="156" y="124" width="72" height="52" rx="10" className="fill-muted" />
      <rect x="72" y="188" width="72" height="52" rx="10" className="fill-muted" />
      <rect x="156" y="188" width="72" height="52" rx="10" className="fill-muted" />
      <circle cx="256" cy="210" r="7" className="fill-ok" />
    </IllustrationFrame>
  );
}

/** Per-id lookup. Throws on unknown id so missing illustrations fail loudly in dev. */
export const SERVICE_ILLUSTRATIONS: Readonly<Record<string, () => ReactNode>> = {
  compute: () => <ComputeIllustration />,
  networking: () => <NetworkingIllustration />,
  storage: () => <StorageIllustration />,
  identity: () => <IdentityIllustration />,
  'quotas-audit': () => <QuotasAuditIllustration />,
  'app-catalog': () => <AppCatalogIllustration />,
};

export function requireServiceIllustration(id: string): () => ReactNode {
  const illustration = SERVICE_ILLUSTRATIONS[id];
  if (!illustration) {
    throw new Error(`No SERVICE_ILLUSTRATIONS entry for id "${id}".`);
  }
  return illustration;
}
