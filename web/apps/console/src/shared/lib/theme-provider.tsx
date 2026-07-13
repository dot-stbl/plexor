// Backward-compat re-export. The full implementation lives in
// preferences-provider.tsx (theme + accent + fontSize in one place).
// main.tsx wraps the app in <PreferencesProvider>; this module just
// re-exports the same provider and hooks for any code that imports
// from the old path.
import type { ComponentProps } from 'react';
import {
  PreferencesProvider,
  useTheme,
  PREFERENCES_DEFAULT,
  type Theme,
} from './preferences-provider';

export function ThemeProvider({
  defaultTheme,
  ...rest
}: ComponentProps<typeof PreferencesProvider> & { defaultTheme?: Theme }) {
  return (
    <PreferencesProvider
      defaultPreferences={defaultTheme ? { theme: defaultTheme } : undefined}
      {...rest}
    />
  );
}

export { useTheme, PREFERENCES_DEFAULT, type Theme };