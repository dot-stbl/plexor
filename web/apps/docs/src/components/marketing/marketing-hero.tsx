import { Link } from '@tanstack/react-router';
import { PlexorMark } from '@plexor/ui/brand';

/**
 * Marketing hero — PlexorMark, a kicker, the operator-facing headline,
 * a short lead paragraph, and two CTAs (primary → docs, secondary →
 * "how it's organized"). The CTAs use TR `Link` so the SPA router
 * stays intact.
 *
 * Voice: second-person, operator vocabulary. The technical surface
 * ("one binary, one process") stays because operators install it;
 * what is removed is the engineering positioning ("no microservices
 * sprawl", "modular monolith"), which belongs in the rationale docs,
 * not on the landing page.
 */
export function MarketingHero() {
  return (
    <section className="border-b border-border">
      <div className="mx-auto max-w-7xl px-6 py-20 md:py-28">
        <div className="flex items-center gap-3">
          <PlexorMark className="h-10 w-10 text-foreground" />
          <span className="font-mono text-xs uppercase tracking-[0.16em] text-muted-2">
            plexor · self-hosted cloud
          </span>
        </div>

        <h1 className="mt-8 max-w-3xl text-4xl font-semibold tracking-tight text-foreground md:text-5xl md:leading-[1.05]">
          Self-hosted cloud for your hardware.
        </h1>

        <p className="mt-6 max-w-2xl text-base leading-7 text-muted-foreground md:text-lg md:leading-8">
          Plexor is a self-hosted cloud platform. Run virtual machines,
          private networks, block and object storage on the servers
          already in your rack — and add Postgres, Redis, Keycloak or
          your own apps from a built-in catalog. One binary, one port,
          one process to keep an eye on.
        </p>

        <div className="mt-10 flex flex-wrap items-center gap-3">
          <Link
            to="/docs/getting-started"
            className="inline-flex h-10 items-center justify-center rounded-md bg-primary px-5 text-sm font-medium text-primary-foreground transition-colors duration-fast ease-out hover:bg-primary/90"
          >
            Open the docs →
          </Link>
          <Link
            to="/docs/concepts"
            className="inline-flex h-10 items-center justify-center rounded-md border border-border bg-transparent px-5 text-sm font-medium text-foreground transition-colors duration-fast ease-out hover:border-foreground/30"
          >
            How it&apos;s organized
          </Link>
        </div>

        <p className="mt-8 font-mono text-xs text-muted-2">
          v0.2 · pre-stable · MVP — single-tenant deploys
        </p>
      </div>
    </section>
  );
}
