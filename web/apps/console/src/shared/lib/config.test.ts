import { afterEach, describe, expect, it } from 'vitest';
import { getBootConfig } from './config';

describe('getBootConfig', () => {
  const original = window.__PLEXOR_CONFIG__;

  afterEach(() => {
    // Restore whatever the host page set up. The test above may have
    // assigned nothing; setting to undefined lets the next test decide.
    if (original === undefined) {
      delete window.__PLEXOR_CONFIG__;
    } else {
      window.__PLEXOR_CONFIG__ = original;
    }
  });

  it('returns the defaults when window.__PLEXOR_CONFIG__ is missing', () => {
    delete window.__PLEXOR_CONFIG__;

    const config = getBootConfig();

    expect(config.brand.name).toBe('Plexor');
    expect(config.brand.logoUrl).toBeNull();
    expect(config.brand.faviconUrl).toBeNull();
    expect(config.theme.defaultPresetId).toBe('plexor-default-light');
  });

  it('returns the defaults when window.__PLEXOR_CONFIG__ is an empty object', () => {
    window.__PLEXOR_CONFIG__ = {};

    const config = getBootConfig();

    expect(config.brand.name).toBe('Plexor');
    expect(config.theme.defaultPresetId).toBe('plexor-default-light');
  });

  it('merges a partial override onto the defaults', () => {
    window.__PLEXOR_CONFIG__ = {
      brand: { name: 'Acme Cloud', logoUrl: '/acme.svg', faviconUrl: null },
      theme: { defaultPresetId: 'plexor-noir' },
    };

    const config = getBootConfig();

    expect(config.brand.name).toBe('Acme Cloud');
    expect(config.brand.logoUrl).toBe('/acme.svg');
    expect(config.brand.faviconUrl).toBeNull();
    expect(config.theme.defaultPresetId).toBe('plexor-noir');
  });

  it('keeps default brand when only the theme is overridden', () => {
    window.__PLEXOR_CONFIG__ = {
      theme: { defaultPresetId: 'plexor-noir' },
    };

    const config = getBootConfig();

    expect(config.brand.name).toBe('Plexor');
    expect(config.theme.defaultPresetId).toBe('plexor-noir');
  });

  it('keeps default theme when only the brand is overridden', () => {
    window.__PLEXOR_CONFIG__ = {
      brand: { name: 'Acme Cloud', logoUrl: '/acme.svg', faviconUrl: '/acme-favicon.svg' },
    };

    const config = getBootConfig();

    expect(config.brand.name).toBe('Acme Cloud');
    expect(config.brand.logoUrl).toBe('/acme.svg');
    expect(config.brand.faviconUrl).toBe('/acme-favicon.svg');
    expect(config.theme.defaultPresetId).toBe('plexor-default-light');
  });

  it('ignores unknown top-level keys (host can ship extras without breaking us)', () => {
    window.__PLEXOR_CONFIG__ = {
      brand: { name: 'Acme', logoUrl: null, faviconUrl: null },
      theme: { defaultPresetId: 'plexor-default-light' },
      // future: api.baseUrl, telemetry.endpoint, …
      somethingElse: { whatever: 42 },
    } as never;

    const config = getBootConfig();

    expect(config.brand.name).toBe('Acme');
    expect(config.theme.defaultPresetId).toBe('plexor-default-light');
  });
});
