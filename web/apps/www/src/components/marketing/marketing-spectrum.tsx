import { StatusPill } from '@/components/ui/status-pill';
import { EYEBROW_CLASS } from '@/components/chrome/panel';

interface SpectrumStop {
  readonly id: string;
  readonly num: string;
  readonly title: string;
  readonly body: string;
  readonly variant: 'ok' | 'warn' | 'idle';
  readonly label: string;
}

/** "Single box to rack" spectrum (spec §3.5) — copy fixed per amendment A7 (stop 03). */
const STOPS: readonly SpectrumStop[] = [
  {
    id: 'single-box',
    num: '01',
    title: 'Single box',
    body: 'One server, one binary. VMs, networks, storage and identity — running today.',
    variant: 'ok',
    label: 'Shipped',
  },
  {
    id: 'small-cluster',
    num: '02',
    title: 'Small cluster',
    body: 'A handful of nodes, shared storage and networking. Where v0.3–v0.5 is headed.',
    variant: 'warn',
    label: 'In progress',
  },
  {
    id: 'fleet',
    num: '03',
    title: 'Fleet',
    body: 'Many nodes, many teams — quotas, audit and org-wide policy at rack scale.',
    variant: 'idle',
    label: 'Design',
  },
];

/**
 * "Single box to rack" spectrum — restyled 2026-09-24 to a plain 3-up
 * card grid (YC-style content panel; product owner dropped the bespoke
 * scroll-linked hairline + wireframe morph in favor of a quieter
 * layout). The landing composition wraps this in
 * `<Panel fill="muted">`; nested `bg-card` tiles read as cards against
 * the muted parent.
 */
export function MarketingSpectrum() {
  return (
    <>
      <div>
        <p className={EYEBROW_CLASS}>Scenarios</p>
        <h2 className="max-w-2xl text-3xl font-extrabold tracking-tight text-foreground">
          From a single box to a rack full of them.
        </h2>
      </div>

      <div className="mt-8 grid grid-cols-1 gap-4 md:grid-cols-3 md:gap-6">
        {STOPS.map((stop) => (
          <div key={stop.id} className="bg-card rounded-2xl p-6">
            <div className="flex items-center justify-between gap-2">
              <span className="font-mono text-xs text-muted-2">{stop.num}</span>
              <StatusPill variant={stop.variant} hideDot size="sm">
                {stop.label}
              </StatusPill>
            </div>
            <h3 className="mt-3 text-base font-semibold text-foreground">{stop.title}</h3>
            <p className="mt-1.5 text-sm leading-6 text-muted-foreground">{stop.body}</p>
          </div>
        ))}
      </div>
    </>
  );
}