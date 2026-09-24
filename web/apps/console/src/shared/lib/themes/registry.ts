import {
  DEFAULT_PRESET_ID,
  presets as builtInPresets,
  listPresets,
  type ThemePreset,
} from '@plexor/ui/themes';
import {
  communityThemes,
  getCommunityTheme as findCommunityTheme,
  type CommunityTheme,
} from './community-themes';

/**
 * Console-side theme registry. Wraps the `@plexor/ui` built-in registry
 * with a community-themes marketplace lookup so operators can activate a
 * marketplace theme alongside the three first-party presets. Built-in
 * IDs always resolve first; unknown IDs throw (stale storage / typo),
 * matching the registry contract.
 *
 * Community layer:
 *   - `listCommunityThemes()` — marketplace display order.
 *   - `getCommunityTheme(id)` — non-throwing miss (used by the marketplace
 *     UI's "activate" path for stale storage values).
 */
export { DEFAULT_PRESET_ID, listPresets };

export function getPreset(id: string): ThemePreset {
  const fromBuiltIn = builtInPresets.find((preset) => preset.id === id);
  if (fromBuiltIn) return fromBuiltIn;
  const fromCommunity = findCommunityTheme(id);
  if (fromCommunity) return fromCommunity;
  throw new Error(`Unknown theme preset: ${id}`);
}

export function listCommunityThemes(): readonly CommunityTheme[] {
  return communityThemes;
}

export function getCommunityTheme(id: string): CommunityTheme | null {
  return findCommunityTheme(id);
}