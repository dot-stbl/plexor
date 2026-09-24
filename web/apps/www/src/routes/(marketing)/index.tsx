import { createFileRoute } from '@tanstack/react-router';
import { SiteFrame } from '@/components/chrome/site-frame';
import { EYEBROW_CLASS, Panel, PanelContainer, PanelStack } from '@/components/chrome/panel';
import { Reveal } from '@/components/motion';
import { MarketingComparison } from '@/components/marketing/marketing-comparison';
import { MarketingCta } from '@/components/marketing/marketing-cta';
import { MarketingFeatures } from '@/components/marketing/marketing-features';
import { MarketingHero } from '@/components/marketing/marketing-hero';
import { MarketingHeroPreview } from '@/components/marketing/marketing-hero-preview';
import { MarketingInstall } from '@/components/marketing/marketing-install';
import { MarketingManifesto } from '@/components/marketing/marketing-manifesto';
import { MarketingReleaseCallout } from '@/components/marketing/marketing-release-callout';
import { MarketingScreens } from '@/components/marketing/marketing-screens';
import { MarketingSpectrum } from '@/components/marketing/marketing-spectrum';

/**
 * Landing page — composition only (YC-informed panel restyle, product
 * owner decision 2026-09-24). The page is a stack of big flat rounded
 * panels (`Panel`, `components/chrome/panel.tsx`) inside one contained
 * column (`PanelContainer`) — solid token fills, no borders, no
 * shadows, small gaps between panels so the page background shows
 * through the seams. This replaces the old full-width
 * `SiteFrame`/`FrameSection` hairline-border rhythm on this page only —
 * `FrameSection` itself is untouched and still backs the header, footer
 * and docs 3-column grid.
 *
 * Order: hero (headline + SVG, no screenshot) → screens (2 console
 * shots, stacked full-width) → services catalog (6 surfaces,
 * category-labelled) → how it runs (light screenshot panel, full width)
 * → get running (inverted terminal panel, its own full-width panel
 * right below) → scenarios (single box / small cluster / fleet) →
 * manifesto → comparison → latest release + docs entry points → final
 * CTA (inverted). The screenshot and terminal panels used to share one
 * `PanelRow` (50/50) — split into two full-width panels instead so the
 * console screenshot reads at a legible size (was shrinking to ~1/4 of
 * its native resolution in that half-width slot). Fills alternate
 * card/muted/sunken/inverted so no two adjacent panels share a tone
 * (see `panel.tsx` for the fill palette). No video, no scroll-scrubbed
 * tour, no canvas background, no more than 3 static screenshots total
 * (product owner feedback, unchanged).
 *
 * Each section still lives in its own component
 * (`components/marketing/*`) so a copy edit lands in one file under 200
 * lines; this file only decides fills, order and panel/row grouping.
 * `Reveal` wraps sections that don't already own a motion story
 * (manifesto/comparison/release) — hero, screens, features, install and
 * spectrum each drive their own effects internally.
 */
export const Route = createFileRoute('/(marketing)/')({
  component: Landing,
  head: () => ({
    meta: [
      { title: 'plexor — self-hosted cloud for your hardware' },
      {
        name: 'description',
        content:
          'Plexor is a self-hosted cloud platform. Run virtual machines, private networks, block and object storage on the servers already in your rack — with identity, quotas, audit, and an app catalog out of the box.',
      },
    ],
  }),
});

function Landing() {
  return (
    <SiteFrame>
      <PanelContainer>
        <PanelStack>
          <Panel fill="card">
            <MarketingHero />
          </Panel>

          <Panel fill="muted">
            <MarketingScreens />
          </Panel>

          <Panel id="services" fill="sunken">
            <MarketingFeatures />
          </Panel>

          <Panel fill="card">
            <p className={EYEBROW_CLASS}>How it runs</p>
            <h2 className="mt-2 max-w-2xl text-3xl font-extrabold tracking-tight text-foreground">
              The real console, on your hardware.
            </h2>
            <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">
              Every action in Plexor goes through the same UI you see here — no
              separate CLI-only surface, no hidden control plane.
            </p>
            <div className="mt-8">
              <MarketingHeroPreview />
            </div>
          </Panel>

          <Panel fill="inverted">
            <MarketingInstall />
          </Panel>

          <Panel fill="muted">
            <MarketingSpectrum />
          </Panel>

          <Panel fill="card">
            <Reveal>
              <MarketingManifesto />
            </Reveal>
          </Panel>

          <Panel fill="sunken">
            <Reveal>
              <MarketingComparison />
            </Reveal>
          </Panel>

          <Panel fill="muted">
            <Reveal>
              <MarketingReleaseCallout />
            </Reveal>
          </Panel>

          <Panel fill="inverted">
            <MarketingCta />
          </Panel>
        </PanelStack>
      </PanelContainer>
    </SiteFrame>
  );
}
