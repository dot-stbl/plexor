import { Link } from '@tanstack/react-router';
import { Button } from '@/components/ui/button';
import { CopyButton } from '@/components/ui/copy-button';
import { GitHubIcon } from '@/components/chrome/github-icon';
import { EYEBROW_CLASS } from '@/components/chrome/panel';
import { CLONE_COMMAND, GITHUB_URL } from '@/components/chrome/nav-config';
import { MarketingHeroIllustration } from './marketing-hero-illustration';

/**
 * Hero — copy on the left, decorative flat SVG illustration on the right
 * (the 2026-09-24 landing restyle). Two-column grid at `lg:`; single stacked
 * column below. Headline bumped to `font-extrabold` (Onest Variable
 * supports the full 100–900 weight range, no font config change). The old
 * mono/uppercase eyebrow is replaced with `EYEBROW_CLASS` from
 * `components/chrome/panel` — plain sentence-case per the restyle spec.
 *
 * The console screenshot that used to live here (`MarketingHeroPreview`)
 * is no longer this file's concern — it lives in the "how it runs" panel
 * rendered by `routes/(marketing)/index.tsx`. The version line lives in
 * the header's version badge (`SiteHeader`), so it isn't said twice on
 * the same viewport. No background canvas / particle field any more
 * (product owner feedback: "the background — particles or whatever it is
 * — is crap, remove it") — the section is plain, no absolutely positioned
 * decorative layer, so no z-index/stacking contract to document here.
 *
 * Column split is `3fr_2fr` (60/40) — a `lg:` 55/45 split combined with
 * `lg:text-7xl` used to wrap the headline to 4 lines at 1280 (an
 * orphaned "own" on its own line): the illustration column was wide
 * enough to undersell, but the text column was too narrow for a 72px
 * headline. Fix is two levers together, not just one: the wider 60%
 * text column, AND a headline size that steps DOWN at `lg:` (two-column
 * layout starts) before stepping back up at `xl:`/`2xl:` as the column's
 * absolute width grows — `text-5xl md:text-6xl lg:text-5xl xl:text-6xl
 * 2xl:text-7xl`. Verified via `bun run shot page / --width 1280` (3
 * lines) and `--width 1920` (2 lines); `text-balance` avoids single-word
 * orphan lines at any width in between. The illustration still reads at
 * confident scale at 40% of the panel width (its own `lg:max-w-none`
 * fills whatever the grid column gives it).
 */
export function MarketingHero() {
  return (
    <div className="lg:grid lg:grid-cols-[3fr_2fr] lg:items-center lg:gap-10 xl:gap-12">
      <div>
        <p className={EYEBROW_CLASS}>Plexor · self-hosted cloud</p>

        <h1 className="mt-4 text-balance text-5xl leading-[1.05] font-extrabold tracking-tight text-foreground md:text-6xl lg:text-5xl xl:text-6xl 2xl:text-7xl">
          The cloud you run on your own hardware.
        </h1>

        <p className="mt-6 max-w-xl text-base leading-7 text-muted-foreground md:text-lg md:leading-8">
          Virtual machines, private networks, block and object storage,
          identity and audit — one binary, on the servers you already own.
          No hosted control plane, no vendor lock-in.
        </p>

        <div className="mt-8 flex flex-wrap items-center gap-3">
          <Button size="lg" render={<Link to="/docs/getting-started">Get started</Link>} />
          <Button variant="outline" size="lg" render={<a href={GITHUB_URL} target="_blank" rel="noreferrer" />}>
            <GitHubIcon className="size-4" />
            GitHub
          </Button>
          <div className="flex items-center gap-2 rounded-md border border-border bg-card px-3 py-2">
            <span className="font-mono text-xs text-muted-foreground">{CLONE_COMMAND}</span>
            <CopyButton value={CLONE_COMMAND} copyLabel="Copy the clone command" />
          </div>
        </div>
      </div>

      <div className="mx-auto mt-12 max-w-md lg:mt-0 lg:max-w-none lg:mx-0">
        <MarketingHeroIllustration />
      </div>
    </div>
  );
}