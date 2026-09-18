/**
 * Community themes registry tests — the marketplace surface that sits on
 * top of the built-in preset registry. The registry reads ALL three
 * community presets (synthwave-night-dark, synthwave-night-light,
 * paper-light) and looks them up by id; the marketplace UI relies on
 * this lookup being consistent.
 */
import { describe, expect, it } from 'vitest';
import {
  communityThemes,
  getCommunityTheme,
  type CommunityTheme,
} from './community-themes';
import { getPreset, listCommunityThemes } from './registry';
import type { ThemePreset } from './presets';

describe('community themes registry', () => {
  it('ships at least two community themes', () => {
    expect(communityThemes.length).toBeGreaterThanOrEqual(2);
  });

  it('every community theme has unique id and is a ThemePreset shape', () => {
    const seen = new Set<string>();
    for (const theme of communityThemes) {
      expect(seen.has(theme.id)).toBe(false);
      seen.add(theme.id);
      // Authored CommunityTheme fields are present + non-empty.
      expect(typeof theme.author).toBe('string');
      expect(theme.author.length).toBeGreaterThan(0);
      expect(typeof theme.version).toBe('string');
      expect(theme.version.length).toBeGreaterThan(0);
      // The marketplace discriminator is the literal `true`.
      expect(theme.marketplace).toBe(true);
    }
  });

  it('every community theme declares the same TokenName vocabulary as built-ins', () => {
    const builtIn = getPreset('plexor-default-light');
    const builtInKeys = Object.keys(builtIn.tokens).sort();
    for (const theme of communityThemes) {
      expect(Object.keys(theme.tokens).sort()).toEqual(builtInKeys);
    }
  });
});

describe('listCommunityThemes', () => {
  it('returns the same array the module exports, in module order', () => {
    expect(listCommunityThemes()).toEqual(communityThemes);
  });

  it('returns at least two themes', () => {
    expect(listCommunityThemes().length).toBeGreaterThanOrEqual(2);
  });
});

describe('getCommunityTheme', () => {
  it('returns the theme for a known id (paper-light)', () => {
    const theme = getCommunityTheme('paper-light');
    expect(theme).not.toBeNull();
    expect(theme?.id).toBe('paper-light');
    // structural compatibility: every CommunityTheme is a ThemePreset too.
    const asPreset: ThemePreset | undefined = theme ?? undefined;
    expect(asPreset?.id).toBe('paper-light');
  });

  it('returns the theme for another known id (synthwave-night-dark)', () => {
    const theme: CommunityTheme | null = getCommunityTheme('synthwave-night-dark');
    expect(theme).not.toBeNull();
    expect(theme?.id).toBe('synthwave-night-dark');
  });

  it('returns null on an unknown id (no throw — caller handles "not installed")', () => {
    expect(getCommunityTheme('not-a-real-marketplace-theme')).toBeNull();
  });
});

describe('getPreset — community extension', () => {
  it('resolves a community theme id via the unified registry lookup', () => {
    const paper = getPreset('paper-light');
    expect(paper.id).toBe('paper-light');
    // Theme shape unchanged — built-in or community, the contract is the same.
    expect(paper.tokens.background).toBeTruthy();
  });

  it('still resolves built-in ids through the unified lookup', () => {
    const light = getPreset('plexor-default-light');
    expect(light.id).toBe('plexor-default-light');
  });

  it('still throws on an id that exists in neither layer', () => {
    expect(() => getPreset('definitely-not-a-preset')).toThrowError(/Unknown theme preset/);
  });
});
