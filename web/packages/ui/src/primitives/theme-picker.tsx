import { useCallback, useState, type ReactNode } from 'react';
import { applyPreset } from '@plexor/ui/themes/apply-tokens';
import { presets, DEFAULT_PRESET_ID, type ThemePreset } from '@plexor/ui/themes/presets';

/**
 * Theme picker — the public surface `@plexor/ui` exposes for switching
 * presets. Listens to localStorage (`plexor-theme`) for cross-page
 * persistence, calls `applyPreset()` to mutate `<html>` at runtime.
 *
 * Extracted from console's `preferences-provider.tsx` for the docs site:
 * the docs surface only needs the three built-in presets (no community
 * themes, no per-user namespaces, no i18n) and doesn't ship the full
 * preferences system (accent + font + language). When a later consumer
 * needs the full surface, this file is the seed for it.
 */
const STORAGE_KEY = 'plexor-theme';

function loadInitialPresetId(): string {
  if (typeof window === 'undefined') return DEFAULT_PRESET_ID;
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (raw && presets.some((preset) => preset.id === raw)) return raw;
  } catch {
    // localStorage unavailable — fall through to default
  }
  return DEFAULT_PRESET_ID;
}

export interface ThemePickerState {
  readonly activePresetId: string;
  readonly activePreset: ThemePreset;
  readonly presets: readonly ThemePreset[];
  readonly setPreset: (id: string) => void;
}

function useThemePickerImpl(): ThemePickerState {
  const [activePresetId, setActivePresetId] = useState<string>(loadInitialPresetId);

  const setPreset = useCallback((id: string) => {
    const preset = presets.find((entry) => entry.id === id);
    if (!preset) return;
    applyPreset(preset);
    setActivePresetId(id);
    try {
      window.localStorage.setItem(STORAGE_KEY, id);
    } catch {
      // localStorage unavailable — preset still applies for this session
    }
  }, []);

  const activePreset = presets.find((preset) => preset.id === activePresetId) ?? presets[0]!;
  return { activePresetId, activePreset, presets, setPreset };
}

export function useThemePicker(): ThemePickerState {
  return useThemePickerImpl();
}

/**
 * Render-prop picker — consumers wrap any chrome (a select, a chip rail, a
 * card grid) and the picker handles selection + applies the chosen preset.
 * The docs site ships its own chrome (see docs landing); this is the
 * behaviour hook that chrome binds to.
 */
export interface ThemePickerProps {
  children: (state: {
    readonly presets: readonly ThemePreset[];
    readonly activePresetId: string;
    readonly onSelect: (id: string) => void;
  }) => ReactNode;
}

export function ThemePicker({ children }: ThemePickerProps) {
  const { activePresetId, presets: list, setPreset } = useThemePickerImpl();
  return <>{children({ presets: list, activePresetId, onSelect: setPreset })}</>;
}