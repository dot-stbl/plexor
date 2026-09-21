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
 */
export const Route = createFileRoute('/(marketing)/')({
  component: Landing,
  head: () => ({
    meta: [
      { title: 'plexor — self-hosted cloud' },
      {
        name: 'description',
        content:
          'One binary. Not thirty services. Plexor is a self-hosted cloud platform — control plane, compute, networking, identity, audit — in one process.',
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