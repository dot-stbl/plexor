import { cn } from '@/lib/utils';
import { EYEBROW_CLASS } from '@/components/chrome/panel';

/**
 * Marketing manifesto — a two-column list of refused patterns (×) and
 * the Plexor replacement (✓). Every row answers "what do you actually
 * do instead?". The landing composition wraps this in a shared
 * `<FrameSection><Reveal>` (`routes/(marketing)/index.tsx`), not here.
 * No background texture behind the rows (the `GridLines` that used to
 * sit here was removed along with `SceneBackground`/`DotGrid` —
 * product owner feedback: dislikes decorative background texture).
 *
 * Refusals are operator-pain, not engineering-internal:
 *   - "no Kubernetes to learn first" (not "no k8s runtime")
 *   - "no chart formats that drift" (not "no extracted YAML reconcilers")
 *   - "no surprise egress" (not "no vendor telemetry")
 *
 * Voice: peer-to-peer between the Plexor team and the operator —
 * explanations, not warnings. Column headers are English to keep the
 * documentation consistent with the rest of the landing.
 *
 * Eyebrow typography: restyled 2026-09-24 to the YC-style sentence-case
 * `EYEBROW_CLASS` (dropped the legacy mono/uppercase/tracking
 * treatment on the landing only).
 */
interface Row {
  readonly no: string;
  readonly yes: string;
}

const ROWS: readonly Row[] = [
  {
    no: 'A platform only the largest cloud teams can deploy — dozens of services to bring up, monitor and keep in step.',
    yes: 'One process you boot once. Add a second one when measured load says so, not before.',
  },
  {
    no: 'A container runtime you have to learn before you can run your first app.',
    yes: 'Containers as a deployment target when you want them, plain processes when you do not.',
  },
  {
    no: 'Chart formats that drift between versions, forks and tools.',
    yes: 'A versioned, signed manifest per app. Install and upgrade is one shell command.',
  },
  {
    no: 'A control plane you did not pick, account lock-in, and credentials you do not own.',
    yes: 'Your hardware, your identity provider, your secrets. Plexor runs on what is already in the rack.',
  },
  {
    no: 'Five different UIs — console, marketplace, billing, audit, docs — for five different jobs.',
    yes: 'One console. Marketplace, audit and metering live as tabs in it.',
  },
  {
    no: 'A billing module that phones home and assumes you wanted metering on by default.',
    yes: 'Usage tracking is opt-in and stays inside the cluster. Nothing leaves without an operator turning it on.',
  },
];

export function MarketingManifesto() {
  return (
    <div>
      <header className="mb-8 max-w-2xl">
        <p className={cn(EYEBROW_CLASS, 'mb-2')}>Manifesto</p>
        <h2 className="mb-2 text-3xl font-extrabold tracking-tight text-foreground">
          What Plexor does not do — and what it does instead.
        </h2>
        <p className="text-sm leading-6 text-muted-foreground">
          A self-hosted cloud should not look like a hosted one. Each row
          below is a refusal and the answer we landed on.
        </p>
      </header>

      <div className="grid grid-cols-1 gap-x-8 md:grid-cols-[1fr_1fr] xl:gap-x-16">
        <div className={cn(EYEBROW_CLASS, 'mb-2 md:pt-1')}>Not this</div>
        <div className={cn(EYEBROW_CLASS, 'mb-2 md:pt-1 md:text-right')}>This instead</div>

        {ROWS.map((row) => (
          <RowPair key={row.no} row={row} />
        ))}
      </div>
    </div>
  );
}

function RowPair({ row }: { row: Row }) {
  return (
    <>
      <div className="border-b border-border/60 py-4 pr-4">
        <p className="text-sm leading-6 text-muted-foreground">
          <span className="mr-2 font-medium text-err">×</span>
          {row.no}
        </p>
      </div>
      <div className="border-b border-border/60 py-4 pl-4">
        <p className="text-sm leading-6 text-foreground">
          <span className="mr-2 font-medium text-ok">✓</span>
          {row.yes}
        </p>
      </div>
    </>
  );
}
