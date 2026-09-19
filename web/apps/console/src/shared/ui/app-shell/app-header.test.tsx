/**
 * AppHeader — breadcrumb home icon micro-interaction.
 *
 * The home icon is the leftmost breadcrumb element on every page; a tiny
 * scale on hover makes the affordance feel responsive without distracting.
 */
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from '@/test-utils';
import { AppHeader } from './app-header';

// jsdom exposes SVGSVGElement.className as an SVGAnimatedString; the live
// string lives on `baseVal`. Plain `?.className` returns an object that
// .toContain() treats as empty, which made the prior probe fail.
function svgClass(svg: Element | null | undefined): string {
  const raw = svg?.getAttribute('class') ?? '';
  return raw;
}

describe('AppHeader — icon micro-interactions', () => {
  it('breadcrumb home link scales up on hover so the icon feels responsive', () => {
    renderWithProviders(<AppHeader />);
    const homeLink = document.body.querySelector('[data-od-id="breadcrumb-home"]');
    expect(homeLink).not.toBeNull();
    const icon = homeLink?.querySelector('svg') ?? null;
    expect(icon).not.toBeNull();
    const cls = svgClass(icon);
    expect(cls).toContain('transition-transform');
    expect(cls).toContain('duration-200');
    expect(cls).toContain('hover:scale-110');
  });

  it('breadcrumb home link transitions colour on hover (foreground shift)', () => {
    renderWithProviders(<AppHeader />);
    const homeLink = document.body.querySelector('[data-od-id="breadcrumb-home"]');
    const cls = homeLink?.className ?? '';
    expect(cls).toContain('transition-colors');
    expect(cls).toContain('hover:text-foreground');
  });
});
