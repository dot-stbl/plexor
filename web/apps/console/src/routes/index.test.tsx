/**
 * Home page — i18n key regression suite.
 *
 * The home page renders the SECTIONS catalog. Every section's `label` and
 * `caption` fields are i18n KEYS (e.g. "nav.sections.compute") — the home
 * page must resolve them through `t()` at render time. Before the fix,
 * the home page rendered the raw key as visible text ("nav.sections.compute"
 * instead of "Compute"), which i18next couldn't recover from because the
 * page itself never called `t()` on the value. Two bugs this file guards
 * against:
 *
 * 1. **Raw key leak.** Each card's title + caption must show the translated
 *    value, not the dotted key string.
 *
 * 2. **Parity with the nav catalog.** The same SECTIONS array feeds both
 *    the home page and the contextual sidebar / launcher. Translating the
 *    keys here matches what the rest of the chrome already shows, so the
 *    user sees the same string everywhere a section appears.
 */
import { describe, expect, it, beforeEach } from 'vitest';
import { renderWithProviders } from '@/test-utils';
import en from '@/shared/lib/i18n/locales/en/common.json';
import { HomePage } from './index';
import { SECTIONS } from '@/shared/ui/app-shell/nav-config';
import { writeSession, type StoredSession } from '@/shared/lib/session';

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

function makeSession(): StoredSession {
  return {
    accessToken: 'jwt.test',
    refreshToken: 'refresh.test',
    expiresAt: Date.now() + 60_000,
    user: {
      id: 'user-1',
      email: 'jane.doe@example.com',
      displayName: 'Jane Doe',
      roles: ['viewer'],
    },
  };
}

function getByOdId(id: string): HTMLElement {
  const el = document.body.querySelector(`[data-od-id="${id}"]`);
  if (!el) throw new Error(`missing element with data-od-id="${id}"`);
  return el as HTMLElement;
}

describe('Home page — section i18n', () => {
  beforeEach(() => {
    // The route's beforeLoad redirects to /login when no session is present;
    // seeding a minimal session lets HomePage render at all.
    writeSession(makeSession());
  });

  it('renders each section card with its translated label and caption, not raw keys', () => {
    renderWithProviders(<HomePage />);
    for (const section of SECTIONS) {
      const card = getByOdId(`home-card-${section.id}`);
      const translatedLabel = tSync(section.label);
      const translatedCaption = tSync(section.caption);
      // Translated value must be present in the card.
      expect(card.textContent).toContain(translatedLabel);
      expect(card.textContent).toContain(translatedCaption);
      // Raw key string must NOT be present — that's the regression we're
      // guarding against. If the page forgot to call t(), the literal
      // "nav.sections.compute" etc. would render to the user.
      expect(card.textContent).not.toContain(section.label);
      expect(card.textContent).not.toContain(section.caption);
    }
  });

  it('does not leak any raw nav.sections.* key onto the page', () => {
    renderWithProviders(<HomePage />);
    // Belt-and-suspenders sweep: even if a future card introduced a third
    // i18n field, none of the section keys may appear in raw form.
    for (const section of SECTIONS) {
      expect(document.body.textContent).not.toContain(section.label);
      expect(document.body.textContent).not.toContain(section.caption);
    }
  });
});
