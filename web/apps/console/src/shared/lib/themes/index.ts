/**
 * Console theme barrel — built-in surface comes from `@plexor/ui`
 * (single source of truth); the community marketplace layer is a console
 * extension that wraps the built-in lookup.
 */
export {
  presets,
  DEFAULT_PRESET_ID as DEFAULT_THEME_PRESET_ID,
  applyPreset,
} from '@plexor/ui/themes';
export type { ThemePreset, TokenName } from '@plexor/ui/themes';
export { type CommunityTheme, communityThemes } from './community-themes';
export {
  DEFAULT_PRESET_ID,
  listPresets,
  listCommunityThemes,
  getCommunityTheme,
  getPreset,
} from './registry';