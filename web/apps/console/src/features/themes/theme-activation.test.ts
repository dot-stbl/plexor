/**
 * Theme activation persistence — localStorage read/write for the
 * operator's chosen community theme id. v1 of the marketplace uses
 * local storage as the durable surface; Phase 5+ replaces these two
 * helpers with a kubb-generated client bound to a per-org tenant
 * row.
 *
 * Tests cover the three contract behaviours:
 *   1. Empty localStorage → `getActiveThemeId()` returns `null`.
 *   2. Set then get → preserves the value.
 *   3. Set null → removes the key (verified via get → null).
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import {
  getActiveThemeId,
  setActiveThemeId,
  THEME_ACTIVATION_STORAGE_KEY,
} from './theme-activation';

describe('theme-activation', () => {
  beforeEach(() => {
    // Fresh storage between tests — every assertion reads/writes the
    // canonical key, so the test order shouldn't matter, but the
    // beforeEach keeps the assertions obvious.
    window.localStorage.clear();
  });

  afterEach(() => {
    window.localStorage.clear();
  });

  it('exposes the canonical storage key for main.tsx + tests', () => {
    expect(THEME_ACTIVATION_STORAGE_KEY).toBe('plexor.theme.activation');
  });

  it('returns null when no theme has been activated', () => {
    expect(getActiveThemeId()).toBeNull();
  });

  it('returns null when localStorage holds an empty string', () => {
    window.localStorage.setItem(THEME_ACTIVATION_STORAGE_KEY, '');
    expect(getActiveThemeId()).toBeNull();
  });

  it('preserves the activated theme id across read/write', () => {
    setActiveThemeId('paper-light');
    expect(getActiveThemeId()).toBe('paper-light');
    expect(window.localStorage.getItem(THEME_ACTIVATION_STORAGE_KEY)).toBe('paper-light');
  });

  it('clears the storage key when setActiveThemeId is called with null', () => {
    setActiveThemeId('synthwave-night-dark');
    expect(getActiveThemeId()).toBe('synthwave-night-dark');

    setActiveThemeId(null);
    expect(getActiveThemeId()).toBeNull();
    expect(window.localStorage.getItem(THEME_ACTIVATION_STORAGE_KEY)).toBeNull();
  });

  it('overwrites an existing value when called twice', () => {
    setActiveThemeId('paper-light');
    setActiveThemeId('synthwave-night-light');
    expect(getActiveThemeId()).toBe('synthwave-night-light');
  });
});
