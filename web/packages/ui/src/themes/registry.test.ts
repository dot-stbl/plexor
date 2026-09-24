import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { applyPreset } from './apply-tokens';
import { getPreset, listPresets } from './registry';

describe('applyPreset', () => {
  beforeEach(() => {
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('style');
    document.documentElement.classList.remove('dark');
  });

  afterEach(() => {
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('style');
    document.documentElement.classList.remove('dark');
  });

  it('sets data-theme and CSS variables for every token in the preset', () => {
    const light = getPreset('plexor-default-light');

    applyPreset(light);

    expect(document.documentElement.dataset.theme).toBe('plexor-default-light');
    for (const [token, value] of Object.entries(light.tokens)) {
      expect(document.documentElement.style.getPropertyValue(`--${token}`)).toBe(value);
    }
  });

  it('toggles the .dark class on when preset.isDarkPreferred is true', () => {
    const dark = getPreset('plexor-default-dark');

    applyPreset(dark);

    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });

  it('toggles the .dark class off when preset.isDarkPreferred is false', () => {
    document.documentElement.classList.add('dark');

    const light = getPreset('plexor-default-light');

    applyPreset(light);

    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('overrides previous preset values when switching presets', () => {
    applyPreset(getPreset('plexor-default-light'));
    expect(document.documentElement.style.getPropertyValue('--background')).toBe(
      'oklch(98% 0.005 250)',
    );

    applyPreset(getPreset('plexor-default-dark'));
    expect(document.documentElement.style.getPropertyValue('--background')).toBe(
      'oklch(17% 0.012 250)',
    );
  });

  it('listPresets exposes the same presets the registry knows about', () => {
    expect(listPresets().length).toBeGreaterThanOrEqual(3);
  });
});
