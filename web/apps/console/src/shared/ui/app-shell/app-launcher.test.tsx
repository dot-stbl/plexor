/**
 * AppLauncher — menu apps regression suite.
 *
 * Two bugs this file guards against:
 *
 * 1. **Empty cards.** The launcher renders the SECTIONS catalog. Cards for
 *    shipped routes use `<Button render={<Link to={page.to} />}>{inner}</Button>`
 *    so the whole row (icon + title + description) collapses into a clickable
 *    Link. The Button primitive used to drop Button-children in its render
 *    branch (only merged className), so non-soon cards rendered with no
 *    content. Only "soon" cards — which take the non-render Button branch —
 *    showed anything.
 *
 *    Asserts: every block card shows its section title + caption, every
 *    shipped page shows its title + description, and the "soon" block
 *    shows the soon badge on its rows.
 *
 * 2. **Sidebar doesn't click.** The launcher's portal root is `fixed inset-0`
 *    so it covers the whole viewport. Without `pointer-events-none` it
 *    catches the click before it can reach the sidebar underneath, even
 *    though the dim overlay starts at `sideOffset`. Clicks on the
 *    launcher panel itself and the dim overlay still work because their
 *    default `pointer-events: auto` overrides the parent.
 *
 *    Asserts: the portal root carries `pointer-events-none` so the
 *    sidebar area remains click-through.
 */
import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import { renderWithProviders } from '@/test-utils';
import { SidebarProvider } from '@/shared/ui/primitives/sidebar';
import en from '@/shared/lib/i18n/locales/en/common.json';
import { AppLauncher } from './app-launcher';
import { SECTIONS } from './nav-config';

// AppLauncher reads the sidebar state (expanded/collapsed) from context to
// dock flush against the sidebar's right edge. SidebarProvider lives INSIDE
// the renderWithProviders wrapper so it's a child of the test router.
function renderLauncher() {
  return renderWithProviders(
    <SidebarProvider defaultOpen>
      <AppLauncher open onOpenChange={() => {}} />
    </SidebarProvider>,
  );
}

// The launcher uses `data-od-id` (Open-Dawn id namespace), not `data-testid`,
// so a thin query helper keeps the assertions below readable.
function getByOdId(id: string): HTMLElement {
  const el = document.body.querySelector(`[data-od-id="${id}"]`);
  if (!el) throw new Error(`missing element with data-od-id="${id}"`);
  return el as HTMLElement;
}

// Resolve a flat i18n key against the en locale used by renderWithProviders.
// The SECTIONS data holds keys like "nav.sections.compute"; we translate them
// ourselves here so the test reads the same values the launcher renders.
function tSync(key: string): string {
  const locale = en as Record<string, unknown>;
  const parts = key.split('.');
  let cur: unknown = locale;
  for (const part of parts) {
    if (cur && typeof cur === 'object' && part in (cur as Record<string, unknown>)) {
      cur = (cur as Record<string, unknown>)[part];
    } else {
      return key;
    }
  }
  return typeof cur === 'string' ? cur : key;
}

describe('AppLauncher — empty cards regression', () => {
  it('renders every block card with its section title and caption', () => {
    renderLauncher();
    for (const section of SECTIONS) {
      const block = getByOdId(`launcher-block-${section.id}`);
      // Resolve the i18n keys the section claims against the en locale,
      // then assert each block shows its own translated label + caption.
      // Before the Button.composeRender fix, all non-soon pages rendered
      // empty (Link with no children), so even the section header text
      // was the only visible content — the page rows were blank.
      const title = tSync(section.label);
      const caption = tSync(section.caption);
      expect(block.textContent).toContain(title);
      expect(block.textContent).toContain(caption);
      // And the block renders at least one page row (icon + title + desc)
      // for the non-soon pages — pick the first shipped `to` row.
      const shipped = section.pages.find((p) => p.to);
      if (shipped) {
        expect(block.textContent).toContain(tSync(shipped.title));
        expect(block.textContent).toContain(tSync(shipped.description));
      }
    }
  });

  it('renders shipped page titles as links (non-soon path through Button render)', () => {
    renderLauncher();
    // `/vms` is the first shipped route in SECTIONS → renders via the
    // Button+Link path. Before the fix, the Link had no children.
    // The launcher now also renders a SUMMARY stat card at `/vms` (label
    // "Virtual machines"), so disambiguate by picking the link whose
    // body includes the section-page description, not the stat label.
    const vmsLinks = screen.getAllByRole('link', { name: /Virtual machines/i });
    const vmsLink = vmsLinks.find((el) => within(el).queryByText('Instances and status'));
    expect(vmsLink).toBeInstanceOf(HTMLAnchorElement);
    // The Link must contain its description too, not just the title.
    expect(within(vmsLink!).getByText('Instances and status')).toBeInTheDocument();
  });

  it('renders the "soon" badge on the soon block (data section)', () => {
    renderLauncher();
    const databases = getByOdId('launcher-block-data');
    // The soon block has `section.soon === true`, so its header carries the pill.
    // Pill text is the Russian word for "soon" (hardcoded in the launcher).
    expect(databases.textContent).toContain('скоро');
  });
});

describe('AppLauncher — sidebar click-through regression', () => {
  it('portal root carries pointer-events-none so the sidebar stays clickable', () => {
    renderLauncher();
    const portal = getByOdId('launcher-portal');
    expect(portal.className).toContain('pointer-events-none');
  });

  it('overlay + menu panel re-enable pointer-events so the launcher itself is clickable', () => {
    renderLauncher();
    const overlay = document.querySelector('[aria-hidden="true"]') as HTMLElement | null;
    const panel = getByOdId('launcher');
    expect(overlay?.className).toContain('pointer-events-auto');
    expect(panel.className).toContain('pointer-events-auto');
  });
});

describe('AppLauncher — SUMMARY cards pull real numbers from the mock fixtures', () => {
  it('renders the three SUMMARY cards with non-placeholder values from makeLauncherSummary', () => {
    renderLauncher();
    // The launcher's SUMMARY row used to render `value="—"` + `context="нет данных"`
    // because the array was static and disconnected from any data source.
    // After commit 1, the SUMMARY array is driven by makeLauncherSummary() —
    // a small fixture-derived helper that reads from `web/apps/console/src/mocks/`.
    // If we see the placeholder values anywhere, the wiring is broken.
    const launcher = getByOdId('launcher');
    expect(launcher.textContent).not.toContain('нет данных');
    // The cards always render the labels (Russian) + a numeric value
    // (FLEET-derived) + a context line. Spot-check that the VM card
    // shows "running of 8 total" — the fleet has 5 running + 1
    // provisioning = 6 active over 8 total.
    expect(launcher.textContent).toContain('running of 8 total');
  });
});

describe('AppLauncher — themed scrollbar', () => {
  it('scroll viewport carries the themed variant class so the custom rail CSS targets it', () => {
    renderLauncher();
    // The launcher is the only consumer of variant="themed". If a future
    // change drops the variant or swaps the wrapper, the test fails with
    // a clear pointer at the missing scrollbar identity rather than
    // silently falling back to the browser default.
    const themedHost = document.querySelector('.plexor-scroll-area-themed') as HTMLElement | null;
    expect(themedHost).not.toBeNull();
    expect(themedHost?.dataset['barVariant']).toBe('themed');
  });

  it('themed host does not also hide the scrollbar (no [scrollbar-width:none] / hidden utility)', () => {
    renderLauncher();
    const themedHost = document.querySelector('.plexor-scroll-area-themed') as HTMLElement | null;
    // Earlier revisions of the launcher hid the native scrollbar entirely
    // ([scrollbar-width:none] + [&::-webkit-scrollbar]:hidden) and shipped
    // a fake ScrollBar element. The themed variant keeps the native bar
    // visible — only its appearance is themed.
    expect(themedHost?.className ?? '').not.toMatch(/scrollbar-width:none/);
    // The viewport no longer carries the hide utility either.
    const viewport = themedHost?.querySelector('[data-slot="scroll-area-viewport"]') as HTMLElement | null;
    expect(viewport?.className ?? '').not.toMatch(/::-webkit-scrollbar\]:hidden/);
  });
});

describe('AppLauncher — icon micro-interactions', () => {
  it('close button rotates 90deg on hover so the X feels reactive', () => {
    renderLauncher();
    const close = screen.getByRole('button', { name: 'Закрыть' });
    const icon = close.querySelector('svg');
    expect(icon).not.toBeNull();
    // jsdom: SVGSVGElement.className is an SVGAnimatedString; live string
    // is on .baseVal. Use getAttribute('class') for plain string compare.
    const cls = icon?.getAttribute('class') ?? '';
    expect(cls).toContain('transition-transform');
    expect(cls).toContain('duration-200');
    expect(cls).toContain('hover:rotate-90');
  });

  it('block-card icon scales up on card hover via group/block-card', () => {
    renderLauncher();
    const block = getByOdId('launcher-block-compute');
    const headerIcon = block.querySelector('svg');
    expect(headerIcon).not.toBeNull();
    const cls = headerIcon?.getAttribute('class') ?? '';
    expect(cls).toContain('transition-transform');
    expect(cls).toContain('duration-200');
    expect(cls).toContain('group-hover/block-card:scale-110');
  });

  it('block-card carries the group/block-card hook so the icon hover fires', () => {
    renderLauncher();
    const block = getByOdId('launcher-block-compute');
    expect(block.className).toContain('group/block-card');
  });

  it('overview row arrow nudges right + darkens on hover', () => {
    renderLauncher();
    const overview = screen.getByRole('link', { name: /Обзор проекта/i });
    const svgs = overview.querySelectorAll('svg');
    const arrow = svgs[svgs.length - 1];
    expect(arrow).not.toBeNull();
    const cls = arrow?.getAttribute('class') ?? '';
    expect(cls).toContain('transition-all');
    expect(cls).toContain('duration-200');
    expect(cls).toContain('group-hover/overview:translate-x-0.5');
    expect(cls).toContain('group-hover/overview:text-foreground');
  });

  it('overview row carries the group/overview hook', () => {
    renderLauncher();
    const overview = screen.getByRole('link', { name: /Обзор проекта/i });
    expect(overview.querySelector('div')?.className ?? '').toContain('group/overview');
  });
});

describe('AppLauncher — i18n summary cards', () => {
  it('does not render the legacy "нет данных" placeholder', () => {
    renderLauncher();
    // Earlier revisions hardcoded the Russian "нет данных" string as the
    // Stat `context` prop because no real data was wired up yet. The summary
    // row now ships mock numbers driven by i18n keys; a regression that
    // drops back to the placeholder must fail this test loudly.
    expect(screen.queryByText('нет данных')).toBeNull();
  });

  it('renders mock summary values translated from i18n keys (VMs card)', () => {
    renderLauncher();
    // "Virtual machines" appears in two places now: the SUMMARY stat card
    // AND the compute section's first nav page. Disambiguate by picking the
    // one whose nearest Stat ancestor carries the fixture-derived value.
    // The card combines the merge parents: the label comes from the i18n
    // key, the value/context from the shared mock fixtures (FLEET counts),
    // so the launcher stays in sync with what the MSW handlers return.
    const vmsLabels = screen.getAllByText('Virtual machines');
    const vmsStat = vmsLabels
      .map((el) => el.closest('[data-slot="stat"]'))
      .find((el): el is HTMLElement => el !== null);
    expect(vmsStat).not.toBeNull();
    // FLEET: 5 running + 1 provisioning = 6 active over 8 total.
    expect(vmsStat?.textContent).toContain('6');
    expect(vmsStat?.textContent).toContain('running of 8 total');
  });

  it('renders the sr-only launcher heading and description', () => {
    renderLauncher();
    // The launcher is the dialog landmark; sr-only heading + paragraph give
    // screen readers a context anchor. Both must come from i18n, not be
    // hardcoded Russian.
    const heading = document.querySelector('h2.sr-only');
    expect(heading?.textContent).toBe('App menu');
    const description = document.querySelector('p.sr-only');
    expect(description?.textContent).toBe('Project sections and quick links');
  });
});
