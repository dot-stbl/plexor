import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import i18n from '@/shared/lib/i18n';

/**
 * User visual preferences. The single source of truth for theme, accent
 * color, font size, and language. Persisted in localStorage and applied
 * to the document as CSS variables / class list on every change.
 *
 * Theme precedence: explicit `theme` value wins; if it's 'system' we
 * follow `prefers-color-scheme` via the inline script in main.tsx
 * (no flash) — this provider just records the user's intent.
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

function applyToDocument(prefs: Preferences) {
  if (typeof document === 'undefined') return;
  const root = document.documentElement;

  // Theme — class list (the inline script in main.tsx already applied
  // the resolved class before mount, so this just keeps it in sync if
  // the user toggles at runtime).
  root.classList.remove('light', 'dark');
  if (prefs.theme === 'system') {
    const mql = window.matchMedia('(prefers-color-scheme: dark)');
    root.classList.add(mql.matches ? 'dark' : 'light');
  } else {
    root.classList.add(prefs.theme);
  }

  // Accent — CSS var override. Defaults are defined in :root in index.css;
  // the user override lands here, scoped to <html>.
  root.style.setProperty('--accent', ACCENT_VALUES[prefs.accent]);
  root.style.setProperty('--accent-foreground', 'oklch(100% 0 0)');

  // Font size — base scale. All Tailwind `text-*` utilities resolve
  // through rem (1rem = font-size on <html>), so changing this scales
  // the entire UI proportionally.
  root.style.fontSize = FONT_SIZE_VALUES[prefs.fontSize];

  // Language — keep i18n in sync with the pref. i18next's own
  // localStorage key 'plexor-lang' is only used on init detection;
  // the pref is the source of truth after that.
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
