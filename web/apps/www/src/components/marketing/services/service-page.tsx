import { SiteFrame } from '@/components/chrome/site-frame';
import { Panel, PanelContainer, PanelStack } from '@/components/chrome/panel';
import { MarketingCta } from '@/components/marketing/marketing-cta';
import { requireServiceContent } from './service-content';
import { ServiceHero } from './service-hero';
import { ServiceCapabilities } from './service-capabilities';
import { ServiceConsolePanel } from './service-console-panel';
import { ServiceRelated } from './service-related';

/**
 * Per-service page — the composition root that all six
 * `/(marketing)/services/<id>/` routes render. Reads only from the
 * already-loaded data files (SERVICE_CONTENT / BENTO_CELLS, owned by
 * chunk 1) and lays the page out as the YC panel stack:
 *
 *   hero (card)   →   capabilities (sunken)   →
 *   [optional console screenshot (muted)]   →   related + others (card)   →
 *   closing CTA (inverted)
 *
 * Fills alternate so no two adjacent panels share a tone (per
 * `panel.tsx`'s own rule). The screenshot panel is skipped entirely when
 * the service has no `screenshot` in its SERVICE_CONTENT entry, so
 * pages without one (networking, storage, identity) simply omit it.
 */
export function ServicePage({ id }: { id: string }) {
  const content = requireServiceContent(id);

  return (
    <SiteFrame>
      <PanelContainer>
        <PanelStack>
          <Panel fill="card">
            <ServiceHero id={id} />
          </Panel>
          <Panel fill="sunken">
            <ServiceCapabilities id={id} />
          </Panel>
          {content.screenshot && (
            <Panel fill="muted">
              <ServiceConsolePanel id={id} />
            </Panel>
          )}
          <Panel fill="card">
            <ServiceRelated id={id} />
          </Panel>
          <Panel fill="inverted">
            <MarketingCta />
          </Panel>
        </PanelStack>
      </PanelContainer>
    </SiteFrame>
  );
}