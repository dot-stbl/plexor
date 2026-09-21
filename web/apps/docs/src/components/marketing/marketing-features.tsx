/**
 * Marketing features grid — three cards that explain the headline
 * capabilities. The card list is local on purpose: three entries,
 * no reuse outside landing, no registry abstraction. Future feature
 * pages can use the same card pattern by lifting this array into a
 * shared model.
 *
 * Each card is one of:
 *  - Plexor.Host (the binary itself)
 *  - App providers (the install surface — providers install/upgrade via
 *    one shell command)
 *  - Themes (preset marketplace — three built-ins, community themes
 *    installable on top)
 */
interface FeatureCard {
  readonly eyebrow: string;
  readonly title: string;
  readonly summary: string;
}

const FEATURES: readonly FeatureCard[] = [
  {
    eyebrow: '01',
    title: 'Plexor.Host',
    summary:
      'A single .NET binary ships the control plane: REST + gRPC, every module, OpenAPI generated on every build. Replicas only when measured load demands it.',
  },
  {
    eyebrow: '02',
    title: 'App providers',
    summary:
      'Install + upgrade = one shell command. provider.yaml is versioned and HMAC-signed. Postgres, Redis, Keycloak, Ghost — and your own.',
  },
  {
    eyebrow: '03',
    title: 'Themes operators pick',
    summary:
      'Three built-in monochrome presets today (paper / midnight / noir). Per-org activation, atomic swap, custom-CSS escape hatch (16 KiB cap).',
  },
];

export function MarketingFeatures() {
  return (
    <section className="border-b border-border">
      <div className="mx-auto max-w-7xl px-6 py-16 md:py-20">
        <p className="mb-3 font-mono text-[11px] font-medium uppercase tracking-[0.16em] text-muted-2">
          What you get
        </p>
        <h2 className="mb-10 max-w-2xl text-3xl font-semibold tracking-tight text-foreground">
          One binary, three concrete capabilities.
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