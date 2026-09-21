/**
 * Marketing manifesto — a two-column list of refused patterns (×) and
 * the Plexor replacement (✓). Every row answers "what do you actually
 * do instead?".
 *
 * Refusals are operator-pain, not engineering-internal:
 *   - "no Kubernetes to learn first" (not "no k8s runtime")
 *   - "no chart formats that drift" (not "no extracted YAML reconcilers")
 *   - "no surprise egress" (not "no vendor telemetry")
 *
 * Voice: peer-to-peer between the Plexor team and the operator —
 * explanations, not warnings. Column headers are English to keep the
 * documentation consistent with the rest of the landing.
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
    <section className="border-b border-border">
      <div className="mx-auto max-w-7xl px-6 py-16 md:py-20">
        <header className="mb-8 max-w-2xl">
          <p className="mb-2 font-mono text-[11px] font-medium uppercase tracking-[0.16em] text-muted-2">
            Manifesto
          </p>
          <h2 className="mb-2 text-3xl font-semibold tracking-tight text-foreground">
            What Plexor does not do — and what it does instead.
          </h2>
          <p className="text-sm leading-6 text-muted-foreground">
            A self-hosted cloud should not look like a hosted one. Each
            row below is a refusal and the answer we landed on.
          </p>
        </header>

        <div className="grid grid-cols-1 gap-x-8 md:grid-cols-[1fr_1fr]">
          <div className="mb-2 font-mono text-[11px] font-medium uppercase tracking-[0.16em] text-muted-2 md:pt-1">
            Not this
          </div>
          <div className="mb-2 font-mono text-[11px] font-medium uppercase tracking-[0.16em] text-muted-2 md:pt-1 md:text-right">
            This instead
          </div>

          {ROWS.map((row) => (
            <RowPair key={row.no} row={row} />
          ))}
        </div>
      </div>
    </section>
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
