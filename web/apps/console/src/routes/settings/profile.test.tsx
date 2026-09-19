/**
 * Settings → Profile route — i18n key regression suite.
 *
 * The page renders four cards (profile, theme, language, security) +
 * a sign-out button. Each card carries its own translated heading.
 * The home page test's pattern (index.test.tsx) is the template here:
 * seed a session, render the page, assert every translated heading
 * shows up under its own data-od-id slot AND that no raw key string
 * leaks to the user.
 *
 * Why a test: the i18n-keys.test.ts file walks the source tree and
 * collects every `t('foo.bar')` literal; if a key isn't in the locale
 * JSON it would render the raw key in production. This test exercises
 * the actual rendered DOM to catch layout-level regressions (heading
 * moved to the wrong card, key swapped between cards, etc.).
 */
import type { ComponentType } from 'react';
import { beforeEach, describe, expect, it } from 'vitest';
import { renderWithProviders } from '@/test-utils';
import { writeSession, type StoredSession } from '@/features/auth/session-storage';
import { Route } from './profile';
import en from '@/shared/lib/i18n/locales/en/common.json';

// `Route.options.component` carries the loader-aware generic type from
// `createFileRoute` — extracting it into a ComponentType simplifies the
// JSX usage in the tests below.
const SettingsProfilePage = Route.options.component as ComponentType;

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

function getByOdId(id: string): HTMLElement {
  const el = document.body.querySelector(`[data-od-id="${id}"]`);
  if (!el) throw new Error(`missing element with data-od-id="${id}"`);
  return el as HTMLElement;
}

describe('Settings → Profile page — i18n', () => {
  beforeEach(() => {
    writeSession(makeSession());
  });

  it('renders the four section cards with their translated headings, not raw keys', () => {
    renderWithProviders(<SettingsProfilePage />);
    const cardExpectations: Array<[string, string]> = [
      ['settings-profile-card', 'settings.profile.heading'],
      ['settings-theme-card', 'settings.theme.heading'],
      ['settings-language-card', 'settings.language.heading'],
      ['settings-security-card', 'settings.security.heading'],
    ];
    for (const [odId, headingKey] of cardExpectations) {
      const card = getByOdId(odId);
      const translated = tSync(headingKey);
      expect(card.textContent).toContain(translated);
      expect(card.textContent).not.toContain(headingKey);
    }
  });

  it('renders the user email from the session under the profile card', () => {
    renderWithProviders(<SettingsProfilePage />);
    const profileCard = getByOdId('settings-profile-card');
    expect(profileCard.textContent).toContain('jane.doe@example.com');
  });

  it('renders the page title and subtitle', () => {
    renderWithProviders(<SettingsProfilePage />);
    expect(document.body.textContent).toContain(tSync('settings.title'));
    expect(document.body.textContent).toContain(tSync('settings.subtitle'));
    expect(document.body.textContent).not.toContain('settings.title');
  });

  it('does not leak any raw settings.* key onto the page', () => {
    renderWithProviders(<SettingsProfilePage />);
    // Belt-and-suspenders: any future heading/key added under the
    // settings.* namespace must not render its raw dotted path.
    const keys = [
      'settings.title',
      'settings.subtitle',
      'settings.profile.heading',
      'settings.profile.email',
      'settings.profile.displayName',
      'settings.profile.signOut',
      'settings.theme.heading',
      'settings.theme.light',
      'settings.theme.dark',
      'settings.theme.system',
      'settings.language.heading',
      'settings.language.comingSoon',
      'settings.security.heading',
      'settings.security.comingSoon',
    ];
    for (const key of keys) {
      expect(document.body.textContent).not.toContain(key);
    }
  });
});
