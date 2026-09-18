import { presets, type ThemePreset } from './presets';
import {
  communityThemes,
  getCommunityTheme as findCommunityTheme,
  type CommunityTheme,
} from './community-themes';

/**
 * The boot-time preset. Used when no user override is in localStorage and
 * when `window.__PLEXOR_CONFIG__.theme.defaultPresetId` is unset or points
 * at an unknown id.
 */
export const DEFAULT_PRESET_ID: string = presets[0]?.id ?? 'plexor-default-light';

/**
 * Look up a preset by id, scanning BOTH built-in presets and community
 * themes. Throws on an unknown id — an unknown id is a programmer error
 * (stale storage, typo in the boot config), not a runtime condition the
 * UI should paper over.
 *
 * The community extension lets boot scripts (main.tsx) accept an id
 * pointing at either layer without a separate lookup helper.
 */
export function getPreset(id: string): ThemePreset {
  const fromBuiltIn = presets.find((preset) => preset.id === id);
  if (fromBuiltIn) return fromBuiltIn;
  const fromCommunity = findCommunityTheme(id);
  if (fromCommunity) return fromCommunity;
  throw new Error(`Unknown theme preset: ${id}`);
}

/** All built-in presets, in the order the picker will list them. */
export function listPresets(): readonly ThemePreset[] {
  return presets;
}

/** All community themes, in marketplace display order. */
export function listCommunityThemes(): readonly CommunityTheme[] {
  return communityThemes;
}

/**
 * Look up a single community theme by id. Returns `null` (not throwing)
 * because the marketplace UI's "Activate" path needs a non-throwing miss
 * to handle a stale storage value gracefully.
 */
export function getCommunityTheme(id: string): CommunityTheme | null {
  return findCommunityTheme(id);
}
