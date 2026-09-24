import { applyPreset, getPreset, presets } from '@plexor/ui/themes';

/** Persisted-theme localStorage key — shared with the inline boot script in `index.html`. */
const STORAGE_KEY = 'plexor-theme';

/** Boot-time preset when no user override is set. Mirrors `presets[0].id`. */
const DEFAULT_PRESET_ID = presets[0]?.id ?? 'plexor-default-light';

/**
 * Read the active theme preset from localStorage (or fall back to the
 * boot default) and apply it to `<html data-theme="...">` on first React
 * paint. Mirrors the inline boot script in `index.html` so the theme is
 * consistent between first paint and the React mount — the inline
 * script reads localStorage synchronously before the bundle loads, and
 * this call re-applies so any preset stored under a key the boot script
 * didn't understand (future compatibility) still kicks in.
 *
 * The inline script is kept as a no-FOUC fallback: it stamps `.dark`
 * and `data-theme-mode` before the first paint. This function reuses
 * the same key and ensures the full token set is written even if the
 * boot script was skipped or didn't recognise the id.
 */
export function applyBootPreset(): void {
  const stored = readStoredPresetId();
  const id = isKnownPresetId(stored) ? stored : DEFAULT_PRESET_ID;
  applyPreset(getPreset(id));
}

function readStoredPresetId(): string | null {
  if (typeof window === 'undefined') return null;
  try {
    return window.localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function isKnownPresetId(value: string | null): value is string {
  if (value === null) return false;
  return presets.some((preset) => preset.id === value);
}