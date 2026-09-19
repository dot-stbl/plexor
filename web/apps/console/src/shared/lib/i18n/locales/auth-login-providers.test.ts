/**
 * Regression guard for the auth.login.providers.* keys added in the
 * feature-flag phase. Two invariants:
 *
 * 1. **Translation parity.** Both locale files carry the same set of
 *    new keys (`auth.login.providersDivider`, plus the four provider
 *    labels). Translation parity for the rest of `auth.login` is
 *    already covered by the umbrella `i18n-keys.test.ts`; this test
 *    specifically calls out the new keys so a future regression
 *    surfaces with a focused failure message.
 *
 * 2. **Design contract.** The literal text matches the design
 *    ("Continue with Google", "Continue with GitHub", etc.) — drift
 *    here is silent: missing keys fall back to the raw i18n path
 *    string, which would be ugly in production. The contract
 *    literal text is what copywriters and PMs negotiated, so it
 *    lives as a real assertion rather than a docstring.
 */
import { describe, expect, it } from 'vitest';
import en from '@/shared/lib/i18n/locales/en/common.json';
import ru from '@/shared/lib/i18n/locales/ru/common.json';

type JsonObject = { [key: string]: unknown };

function* collectKeys(obj: JsonObject, prefix = ''): Generator<string> {
  for (const [key, value] of Object.entries(obj)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
      yield* collectKeys(value as JsonObject, path);
    } else {
      yield path;
    }
  }
}

function resolve(obj: JsonObject, path: string): string | undefined {
  const segments = path.split('.');
  let cursor: unknown = obj;
  for (const segment of segments) {
    if (cursor === null || typeof cursor !== 'object') return undefined;
    cursor = (cursor as Record<string, unknown>)[segment];
  }
  return typeof cursor === 'string' ? cursor : undefined;
}

const NEW_KEYS = [
  'auth.login.providersDivider',
  'auth.login.providers.google',
  'auth.login.providers.github',
  'auth.login.providers.oidc',
  'auth.login.providers.ldap',
] as const;

const EN_TEXT: Record<(typeof NEW_KEYS)[number], string> = {
  'auth.login.providersDivider': 'or',
  'auth.login.providers.google': 'Continue with Google',
  'auth.login.providers.github': 'Continue with GitHub',
  'auth.login.providers.oidc': 'Continue with SSO',
  'auth.login.providers.ldap': 'Continue with LDAP',
};

const RU_TEXT: Record<(typeof NEW_KEYS)[number], string> = {
  'auth.login.providersDivider': 'или',
  'auth.login.providers.google': 'Продолжить с Google',
  'auth.login.providers.github': 'Продолжить с GitHub',
  'auth.login.providers.oidc': 'Продолжить через SSO',
  'auth.login.providers.ldap': 'Продолжить с LDAP',
};

describe('auth.login.providers — i18n', () => {
  it('en and ru share the new provider keys', () => {
    const enKeys = new Set(collectKeys(en));
    const ruKeys = new Set(collectKeys(ru));

    const missingInEn = NEW_KEYS.filter((k) => !enKeys.has(k));
    const missingInRu = NEW_KEYS.filter((k) => !ruKeys.has(k));

    expect(missingInEn).toEqual([]);
    expect(missingInRu).toEqual([]);
  });

  it.each(NEW_KEYS)('en copy for "%s" matches the design contract', (key) => {
    expect(resolve(en, key)).toBe(EN_TEXT[key]);
  });

  it.each(NEW_KEYS)('ru copy for "%s" matches the design contract', (key) => {
    expect(resolve(ru, key)).toBe(RU_TEXT[key]);
  });
});