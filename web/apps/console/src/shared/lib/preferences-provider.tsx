import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import i18n from '@/shared/lib/i18n';
import { applyPreset } from '@/shared/lib/themes/apply-tokens';
import { getPreset, DEFAULT_PRESET_ID as REGISTRY_DEFAULT_PRESET_ID } from '@/shared/lib/themes/registry';

/**
 * User visual preferences. The single source of truth for theme, accent
 * color, font size, and language. Persisted in localStorage and applied
 * to the document as CSS variables / class list on every change.
 *
 * Theme precedence: explicit `theme` value wins; if it's 'system' we
 * follow `prefers-color-scheme` via the inline script in main.tsx
 * (no flash) — this provider just records the user's intent.
 *
 * Theme presets (v1 of the registry): the `theme` picker maps to one of
 * the two default presets in `themes/presets.ts`. The boot config's
 * `theme.defaultPresetId` is read on mount but only consumed by the
 * future preset-picker UI (next commit); in v1 the picker is the only
 * way to switch themes and it always resolves to `plexor-default-light`
 * or `plexor-default-dark`.
 *
 * Language: also persisted here (NOT only in i18next's own 'plexor-lang'
 * key). This is the single source of truth — i18n is synced via
 * i18n.changeLanguage() on every change.
 */
export type Theme = 'light' | 'dark' | 'system';
export type Accent = 'plexor' | 'blue' | 'green' | 'orange' | 'pink';
export type FontSize = 'small' | 'medium' | 'large';
export type Language = 'en' | 'ru';

export interface Preferences {
  theme: Theme;
  accent: Accent;
  fontSize: FontSize;
  language: Language;
}

export const PREFERENCES_DEFAULT: Preferences = {
  theme: 'system',
  accent: 'plexor',
  fontSize: 'medium',
  language: 'en',
};

const STORAGE_KEY = 'plexor-preferences';

const ACCENT_VALUES: Record<Accent, string> = {
  plexor: 'oklch(28% 0.02 255)',          // default — dark monochrome ink
  blue:   'oklch(55% 0.18 252)',
  green:  'oklch(58% 0.15 155)',
  orange: 'oklch(68% 0.16 50)',
  pink:   'oklch(64% 0.18 0)',
};

const FONT_SIZE_VALUES: Record<FontSize, string> = {
  small:  '14px',
  medium: '16px',
  large:  '18px',
};

interface PreferencesContextValue {
  preferences: Preferences;
  setPreferences: (next: Preferences) => void;
  update: <K extends keyof Preferences>(key: K, value: Preferences[K]) => void;
  reset: () => void;
}

const PreferencesContext = createContext<PreferencesContextValue | null>(null);

function loadFromStorage(): Preferences {
  if (typeof window === 'undefined') return PREFERENCES_DEFAULT;
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return PREFERENCES_DEFAULT;
    const parsed = JSON.parse(raw) as Partial<Preferences>;
    const theme: Theme =
      parsed.theme === 'light' || parsed.theme === 'dark' || parsed.theme === 'system'
        ? parsed.theme
        : PREFERENCES_DEFAULT.theme;
    const accent: Accent = parsed.accent && parsed.accent in ACCENT_VALUES
      ? (parsed.accent as Accent)
      : PREFERENCES_DEFAULT.accent;
    const fontSize: FontSize =
      parsed.fontSize && parsed.fontSize in FONT_SIZE_VALUES
        ? (parsed.fontSize as FontSize)
        : PREFERENCES_DEFAULT.fontSize;
    const language: Language =
      parsed.language === 'en' || parsed.language === 'ru'
        ? parsed.language
        : (i18n.language as Language) || PREFERENCES_DEFAULT.language;
    return { theme, accent, fontSize, language };
  } catch {
    return PREFERENCES_DEFAULT;
  }
}

/**
 * Map the `light | dark | system` picker value to a preset id.
 *
 * v1 ships three presets (`plexor-default-light`, `plexor-default-dark`,
 * `plexor-noir`); only the first two are reachable through the picker.
 * The boot config's `theme.defaultPresetId` is consumed by
 * `getBootConfig()` (called from `main.tsx`'s favicon IIFE) and will
 * become the seed for the preset picker UI added in the next commit.
 */
function presetIdForMode(theme: Theme, systemPrefersDark: boolean): string {
  if (theme === 'system') {
    return systemPrefersDark ? 'plexor-default-dark' : 'plexor-default-light';
  }
  return theme === 'dark' ? 'plexor-default-dark' : 'plexor-default-light';
}

function applyToDocument(prefs: Preferences) {
  if (typeof document === 'undefined') return;
  const root = document.documentElement;

  // 1. Theme preset — sets tokens, data-theme attribute, and the .dark
  //    Tailwind class. The preset's `isDarkPreferred` drives the class.
  const systemPrefersDark =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-color-scheme: dark)').matches;
  const presetId = presetIdForMode(prefs.theme, systemPrefersDark);
  let preset;
  try {
    preset = getPreset(presetId);
  } catch {
    // Unknown preset id (e.g. a removed preset in storage) — fall back to
    // the registry default so we always render something rather than
    // crash on a stale id.
    preset = getPreset(REGISTRY_DEFAULT_PRESET_ID);
  }
  applyPreset(preset);

  // 2. Accent — user override on top of the preset's --accent. The preset
  //    already wrote its own value; the picker lets the user swap it.
  root.style.setProperty('--accent', ACCENT_VALUES[prefs.accent]);
  root.style.setProperty('--accent-foreground', 'oklch(100% 0 0)');

  // 3. Font size — base scale. All Tailwind `text-*` utilities resolve
  //    through rem (1rem = font-size on <html>), so changing this scales
  //    the entire UI proportionally.
  root.style.fontSize = FONT_SIZE_VALUES[prefs.fontSize];

  // 4. Language — keep i18n in sync with the pref. i18next's own
  //    localStorage key 'plexor-lang' is only used on init detection;
  //    the pref is the source of truth after that.
  if (i18n.language !== prefs.language) {
    void i18n.changeLanguage(prefs.language);
  }
}

export interface PreferencesProviderProps {
  children: ReactNode;
  defaultPreferences?: Partial<Preferences>;
  storageKey?: string;
}

export function PreferencesProvider({
  children,
  defaultPreferences,
  storageKey = STORAGE_KEY,
}: PreferencesProviderProps) {
  const [preferences, setPreferencesState] = useState<Preferences>(() => {
    const loaded = loadFromStorage();
    return { ...PREFERENCES_DEFAULT, ...defaultPreferences, ...loaded };
  });

  // Persist + apply to document on every change.
  useEffect(() => {
    if (typeof window === 'undefined') return;
    try {
      window.localStorage.setItem(storageKey, JSON.stringify(preferences));
    } catch {
      // ignore (private mode, quota exceeded)
    }
    applyToDocument(preferences);
  }, [preferences, storageKey]);

  // React to system theme changes while in 'system' mode.
  useEffect(() => {
    if (preferences.theme !== 'system') return;
    const mql = window.matchMedia('(prefers-color-scheme: dark)');
    const handler = () => applyToDocument(preferences);
    mql.addEventListener('change', handler);
    return () => mql.removeEventListener('change', handler);
  }, [preferences]);

  const setPreferences = useCallback((next: Preferences) => {
    setPreferencesState(next);
  }, []);

  const update = useCallback(
    <K extends keyof Preferences>(key: K, value: Preferences[K]) => {
      setPreferencesState((prev) => ({ ...prev, [key]: value }));
    },
    [],
  );

  const reset = useCallback(() => {
    setPreferencesState({ ...PREFERENCES_DEFAULT, ...defaultPreferences });
  }, [defaultPreferences]);

  const value: PreferencesContextValue = {
    preferences,
    setPreferences,
    update,
    reset,
  };

  return <PreferencesContext.Provider value={value}>{children}</PreferencesContext.Provider>;
}

export function usePreferences(): PreferencesContextValue {
  const ctx = useContext(PreferencesContext);
  if (ctx === null) {
    throw new Error('usePreferences must be used within a PreferencesProvider');
  }
  return ctx;
}

// Re-export the legacy `useTheme` hook so existing call sites (theme-toggle,
// settings modal) keep working. It just reads the `theme` field.
export function useTheme() {
  const { preferences, update, reset } = usePreferences();
  return {
    theme: preferences.theme,
    setTheme: (next: Preferences['theme']) => update('theme', next),
    reset,
  };
}
