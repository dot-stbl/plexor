import { describe, expect, it } from 'vitest';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, resolve, extname } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Regression guard for the i18n locale JSON files.
 *
 * Three failure modes this test catches:
 *
 * 1. Flat dotted keys — i18next treats `.` as a namespace separator, so
 *    a key like `"nav.sections.network": "Network"` is interpreted as
 *    three nested levels. The lookup `t('nav.sections.network')` then
 *    fails and i18next returns the raw key path. Both locale files
 *    must use proper nested object structure for keys with `.` in the
 *    name. The previous PR `feat/fe/themes` had to fix this for the
 *    Theme Marketplace cards; this test prevents it regressing.
 *
 * 2. Missing-translation parity — every leaf key in en/common.json must
 *    have a matching leaf key in ru/common.json (and vice versa). A key
 *    present in only one locale falls back to that one locale's value
 *    via fallbackLng: 'en', which renders English copy inside the
 *    Russian UI. Silent gaps are worse than crashes — this test makes
 *    them loud.
 *
 * 3. Source references resolve — every `t('foo.bar.baz')` literal in
 *    the console app's source must resolve to a leaf in en/common.json.
 *    Without this check, an added component referencing a key that
 *    wasn't added to the locale would render the raw key in production
 *    (no compile error, no runtime error). Walks apps/console/src
 *    excluding this test file and any *.test.* companion.
 *
 * Kept as a structural test (no React, no jsdom needed) so it runs in
 * the default Vitest unit run with no extra config.
 */

const HERE = dirname(fileURLToPath(import.meta.url));
const SRC_ROOT = resolve(HERE, '..', '..', '..', '..');

type JsonObject = { [key: string]: unknown };

function readLocale(locale: 'en' | 'ru'): JsonObject {
  const path = resolve(HERE, locale, 'common.json');
  return JSON.parse(readFileSync(path, 'utf8')) as JsonObject;
}

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

/**
 * Find any JSON object key whose NAME contains `.` and whose VALUE is a
 * string. Such keys are the flat-key bug — i18next would treat the `.`
 * as a path separator and the lookup would fail.
 */
function findFlatDottedStringKeys(obj: JsonObject, prefix = ''): string[] {
  const flat: string[] = [];
  for (const [key, value] of Object.entries(obj)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (key.includes('.') && typeof value === 'string') {
      flat.push(path);
    }
    if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
      flat.push(...findFlatDottedStringKeys(value as JsonObject, path));
    }
  }
  return flat;
}

/**
 * Walk the console app src tree and collect every literal `t('...')`
 * key reference. Matches `t('foo.bar')` and `t("foo.bar")` with a
 * single-quoted or double-quoted string argument. Excludes test files
 * (they legitimately reference test fixtures, not user copy) and this
 * file itself (its helper strings would self-reference).
 */
function collectSourceKeyReferences(): string[] {
  const callRe = /\bt\(\s*['"]([^'"]+)['"]/g;
  const keys: string[] = [];
  walkSrc(SRC_ROOT, (file) => {
    const text = readFileSync(file, 'utf8');
    let match: RegExpExecArray | null;
    while ((match = callRe.exec(text))) {
      keys.push(match[1]);
    }
  });
  return keys;
}

function walkSrc(dir: string, visit: (file: string) => void): void {
  for (const entry of readdirSync(dir)) {
    const p = join(dir, entry);
    const s = statSync(p);
    if (s.isDirectory()) {
      walkSrc(p, visit);
      continue;
    }
    if (extname(p) !== '.ts' && extname(p) !== '.tsx') continue;
    if (p.endsWith('.test.ts') || p.endsWith('.test.tsx')) continue;
    if (p === fileURLToPath(import.meta.url)) continue;
    visit(p);
  }
}

const en = readLocale('en');
const ru = readLocale('ru');
const enKeys = new Set(collectKeys(en));
const ruKeys = new Set(collectKeys(ru));

describe('i18n locale files', () => {
  it('en/common.json has no flat dotted string keys (i18next treats . as separator)', () => {
    const flat = findFlatDottedStringKeys(en);
    expect(flat).toEqual([]);
  });

  it('ru/common.json has no flat dotted string keys', () => {
    const flat = findFlatDottedStringKeys(ru);
    expect(flat).toEqual([]);
  });

  it('en and ru share the same set of leaf keys (translation parity)', () => {
    const onlyInEn = [...enKeys].filter((k) => !ruKeys.has(k)).sort();
    const onlyInRu = [...ruKeys].filter((k) => !enKeys.has(k)).sort();

    expect({ onlyInEn, onlyInRu }).toEqual({ onlyInEn: [], onlyInRu: [] });
  });

  it('every t("...") reference in source resolves to a defined key in en/common.json', () => {
    const refs = collectSourceKeyReferences();
    const uniqueRefs = [...new Set(refs)].sort();
    const missing = uniqueRefs.filter((k) => !enKeys.has(k));
    expect(missing).toEqual([]);
  });
});
