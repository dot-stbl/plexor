import { createFileRoute } from '@tanstack/react-router';
import { MarketingHero } from '@/components/marketing/marketing-hero';
import { MarketingFeatures } from '@/components/marketing/marketing-features';
import { MarketingManifesto } from '@/components/marketing/marketing-manifesto';
import { MarketingRoadmap } from '@/components/marketing/marketing-roadmap';
import { MarketingCta } from '@/components/marketing/marketing-cta';

/**
 * Landing page — composition only. Each section lives in its own
 * component so a future copy edit (e.g. trimming the manifesto) lands
 * in a single file under 200 lines, not a monolithic landing.tsx.
 *
 * Voice: operator, not engineer. The meta description tells a search-
 * result reader what Plexor does for the person running it — not what
 * it is built on. The lead is the eight product surfaces (compute,
 * networking, storage, identity, marketplace, quotas, audit, console
 * theming) — themes is one of them, not the headline.
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
    <>
      <MarketingHero />
      <MarketingFeatures />
      <MarketingManifesto />
      <MarketingRoadmap />
      <MarketingCta />
    </>
  );
}
