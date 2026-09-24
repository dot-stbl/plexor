import { presets, type ThemePreset } from './presets';

/**
 * The boot-time preset. Used when no user override is in localStorage and
 * when `window.__PLEXOR_CONFIG__.theme.defaultPresetId` is unset or points
 * at an unknown id.
 */
export const DEFAULT_PRESET_ID: string = presets[0]?.id ?? 'plexor-default-light';

/**
 * Look up a preset by id. Throws on an unknown id — an unknown id is a
 * programmer error (stale storage, typo in the boot config), not a runtime
 * condition the UI should paper over.
 *
 * Console extends this in `console/src/shared/lib/themes/registry.ts` to
 * also check the community-themes marketplace layer; this package only
 * ships the built-in registry.
 */
export function getPreset(id: string): ThemePreset {
  const fromBuiltIn = presets.find((preset) => preset.id === id);
  if (fromBuiltIn) return fromBuiltIn;
  throw new Error(`Unknown theme preset: ${id}`);
}

/** All built-in presets, in the order the picker will list them. */
export function listPresets(): readonly ThemePreset[] {
  return presets;
}