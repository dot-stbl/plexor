import { describe, expect, it } from 'vitest';
import { presets, DEFAULT_PRESET_ID, type TokenName } from './presets';
import { getPreset, listPresets, DEFAULT_PRESET_ID as REGISTRY_DEFAULT } from './registry';

describe('presets registry', () => {
  it('exports at least three presets and a matching default id', () => {
    expect(presets.length).toBeGreaterThanOrEqual(3);
    expect(DEFAULT_PRESET_ID).toBe('plexor-default-light');
    expect(REGISTRY_DEFAULT).toBe(DEFAULT_PRESET_ID);
  });

  it('ships plexor-default-light, plexor-default-dark, and plexor-noir', () => {
    const ids = presets.map((preset) => preset.id);
    expect(ids).toContain('plexor-default-light');
    expect(ids).toContain('plexor-default-dark');
    expect(ids).toContain('plexor-noir');
  });

  it('every preset has the SAME set of token keys (no preset adds or omits a key)', () => {
    const vocabularies = presets.map((preset) =>
      Object.keys(preset.tokens).sort(),
    );
    const first = vocabularies[0];
    expect(first).toBeDefined();
    for (const vocab of vocabularies) {
      expect(vocab).toEqual(first);
    }
  });

  it('the vocabulary is the documented TokenName union, no extras', () => {
    // Cross-check that every key in every preset is a declared TokenName
    // AND that the vocabulary covers every declared TokenName. A preset
    // that introduced a new key, or a TokenName that no preset ships,
    // would either be silent drift or a compile-time miss.
    const expectedKeys: readonly TokenName[] = [
      'background', 'card', 'popover', 'secondary', 'muted',
      'foreground', 'card-foreground', 'popover-foreground', 'secondary-foreground', 'muted-foreground',
      'border', 'border-2', 'input',
      'surface-2', 'surface-3', 'fg-2', 'muted-2',
      'accent', 'accent-foreground', 'ring',
      'destructive', 'destructive-foreground',
      'ok', 'ok-soft', 'ok-ink',
      'err', 'err-soft', 'err-ink',
      'warn', 'warn-soft', 'warn-ink',
      'idle', 'idle-soft', 'idle-ink',
      'info',
      'chart-1', 'chart-2', 'chart-3', 'chart-4', 'chart-5',
      'sidebar', 'sidebar-foreground', 'sidebar-primary', 'sidebar-primary-foreground',
      'sidebar-accent', 'sidebar-accent-foreground', 'sidebar-border', 'sidebar-ring',
      'radius',
    ];
    const expected = [...expectedKeys].sort();
    for (const preset of presets) {
      expect(Object.keys(preset.tokens).sort()).toEqual(expected);
    }
  });

  it('every preset declares its isDarkPreferred flag', () => {
    for (const preset of presets) {
      expect(typeof preset.isDarkPreferred).toBe('boolean');
    }
  });

  it('default presets carry the OKLCH values currently in index.css', () => {
    // Regression protection: any change to :root in index.css must also
    // land here, and vice versa. The values below are the verbatim
    // :root / .dark declarations as of this commit.
    const light = getPreset('plexor-default-light');
    expect(light.tokens.background).toBe('oklch(98% 0.005 250)');
    expect(light.tokens.foreground).toBe('oklch(22% 0.02 240)');
    expect(light.tokens.accent).toBe('oklch(28% 0.02 255)');
    expect(light.tokens.destructive).toBe('oklch(58% 0.20 25)');

    const dark = getPreset('plexor-default-dark');
    expect(dark.tokens.background).toBe('oklch(17% 0.012 250)');
    expect(dark.tokens.foreground).toBe('oklch(94% 0.006 250)');
    expect(dark.tokens.accent).toBe('oklch(92% 0.006 250)');
    expect(dark.tokens.destructive).toBe('oklch(68% 0.18 25)');
  });

  it('noir is dark-preferred and starts darker than default-dark', () => {
    const noir = getPreset('plexor-noir');
    const dark = getPreset('plexor-default-dark');
    expect(noir.isDarkPreferred).toBe(true);
    // Noir background lightness < default-dark background lightness.
    // Parsing OKLCH strings by hand: leading `oklch(` then a percentage.
    const lightnessOf = (value: string): number => {
      const match = value.match(/oklch\(\s*([\d.]+)/);
      if (!match) throw new Error(`Not an oklch() literal: ${value}`);
      return parseFloat(match[1] as string);
    };
    expect(lightnessOf(noir.tokens.background)).toBeLessThan(
      lightnessOf(dark.tokens.background),
    );
    // And brighter at the foreground, so the gap is wider too.
    expect(lightnessOf(noir.tokens.foreground)).toBeGreaterThan(
      lightnessOf(dark.tokens.foreground),
    );
  });
});

describe('getPreset', () => {
  it('returns the preset for a known id', () => {
    expect(getPreset('plexor-default-light').id).toBe('plexor-default-light');
    expect(getPreset('plexor-noir').id).toBe('plexor-noir');
  });

  it('throws on an unknown id', () => {
    expect(() => getPreset('not-a-real-preset')).toThrowError(/Unknown theme preset/);
  });
});

describe('listPresets', () => {
  it('returns the same presets in the same order as the exported array', () => {
    const listed = listPresets();
    expect(listed).toEqual(presets);
    expect(listed[0]?.id).toBe(DEFAULT_PRESET_ID);
  });
});
