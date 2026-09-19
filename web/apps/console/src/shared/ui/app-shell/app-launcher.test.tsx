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
    const vmsLink = screen.getByRole('link', { name: /Virtual machines/i });
    expect(vmsLink).toBeInstanceOf(HTMLAnchorElement);
    // The Link must contain its description too, not just the title.
    expect(within(vmsLink).getByText('Instances and status')).toBeInTheDocument();
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
