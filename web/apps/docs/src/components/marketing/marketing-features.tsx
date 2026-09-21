/**
 * Marketing features grid — three cards that spell out what Plexor
 * hands you on day one. The card list is local on purpose: three
 * entries, no reuse outside landing, no registry abstraction. Future
 * feature pages can use the same card pattern by lifting this array
 * into a shared model.
 *
 * Each card answers "what does the operator get?" — not "what module
 * shipped?". Internal architecture (Plexor.Host, providers, themes)
 * is named only when the operator touches it from the UI or CLI.
 *
 *   01 — Compute, network and storage on your existing servers
 *   02 — A catalog of ready-to-run apps
 *   03 — Pick the look you run Plexor in
 */
interface FeatureCard {
  readonly eyebrow: string;
  readonly title: string;
  readonly summary: string;
}

const FEATURES: readonly FeatureCard[] = [
  {
    eyebrow: '01',
    title: 'Compute, network and storage on your hardware',
    summary:
      'Virtual machines, private networks, security groups, floating IPs, load balancers, block volumes and S3 buckets — all driven through one binary on the servers you already own.',
  },
  {
    eyebrow: '02',
    title: 'A catalog of ready-to-run apps',
    summary:
      'Postgres, Redis, Keycloak, WordPress, Ghost and a handful of others ship as installable templates. Install or upgrade with one shell command. Add your own by dropping a manifest into the catalog.',
  },
  {
    eyebrow: '03',
    title: 'Pick the look you run Plexor in',
    summary:
      'Three built-in palettes out of the box (light, dark and a blue-tinted dark mode). Operators choose per organization — atomic swap, no restart, with a small CSS escape hatch for the rest.',
  },
];

export function MarketingFeatures() {
  return (
    <section className="border-b border-border">
      <div className="mx-auto max-w-7xl px-6 py-16 md:py-20">
        <p className="mb-3 font-mono text-[11px] font-medium uppercase tracking-[0.16em] text-muted-2">
          What you get on day one
        </p>
        <h2 className="mb-10 max-w-2xl text-3xl font-semibold tracking-tight text-foreground">
          Three things you can do the moment Plexor starts.
        </h2>

        <ul className="grid grid-cols-1 gap-4 md:grid-cols-3">
          {FEATURES.map((feature) => (
            <li
              key={feature.eyebrow}
              className="rounded-lg border border-border bg-card p-6"
            >
              <div className="mb-3 font-mono text-[10px] uppercase tracking-[0.16em] text-muted-2">
                {feature.eyebrow}
              </div>
              <h3 className="mb-2 text-base font-semibold text-foreground">
                {feature.title}
              </h3>
              <p className="text-sm leading-6 text-muted-foreground">
                {feature.summary}
              </p>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
