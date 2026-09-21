import { Link } from '@tanstack/react-router';
import { PlexorMark } from '@plexor/ui/brand';

/**
 * Marketing hero — PlexorMark (oversized), a kicker, the headline, a
 * short lead paragraph, and two CTAs. The primary CTA is the docs
 * entry point; the secondary is the architecture page. Both use TR `Link`
 * to preserve the SPA router (no full reloads).
 *
 * Layout: max-width container, generous vertical padding on both sides
 * so the hero reads as "the page" rather than "the top of a long
 * article". Subsequent sections inherit the same horizontal padding.
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
          One binary. Not thirty services.
        </h1>

        <p className="mt-6 max-w-2xl text-base leading-7 text-muted-foreground md:text-lg md:leading-8">
          Plexor is a self-hosted cloud platform: control plane, compute,
          networking, identity, audit — in one .NET binary. Modular
          monolith, extraction-ready by measured bottleneck, not
          pre-emptively.
        </p>

        <div className="mt-10 flex flex-wrap items-center gap-3">
          <Link
            to="/docs/getting-started"
            className="inline-flex h-10 items-center justify-center rounded-md bg-primary px-5 text-sm font-medium text-primary-foreground transition-colors duration-fast ease-out hover:bg-primary/90"
          >
            Start with the docs →
          </Link>
          <Link
            to="/docs/concepts"
            className="inline-flex h-10 items-center justify-center rounded-md border border-border bg-transparent px-5 text-sm font-medium text-foreground transition-colors duration-fast ease-out hover:border-foreground/30"
          >
            Read the architecture
          </Link>
        </div>

        <p className="mt-8 font-mono text-xs text-muted-2">
          v0.2 · pre-stable · single-tenant deploys only
        </p>
      </div>
    </section>
  );
}