import { useLayoutEffect, useState } from 'react';
import { presets } from '@plexor/ui/themes';

export type ThemeName = 'light' | 'dark';

/**
 * Resolve the active theme name from `<html data-theme="...">`.
 *
 * `apply-tokens.ts` (`@plexor/ui/themes`) writes the preset's *id* to
 * `data-theme` (`plexor-default-light`, `plexor-default-dark`,
 * `plexor-noir`, ...), not a bare `light`/`dark` string. This looks the
 * id up in the preset registry and reads `isDarkPreferred` — the same
 * flag `applyPreset` uses to toggle the Tailwind `.dark` class — so
 * `plexor-noir` (a dark-only preset) correctly resolves to `'dark'`.
 * Falls back to the `.dark` class itself if the id isn't recognised
 * (e.g. a future preset this hook hasn't been updated for).
 */
function resolveThemeName(): ThemeName {
  if (typeof document === 'undefined') return 'light';
  const root = document.documentElement;
  const preset = presets.find((candidate) => candidate.id === root.dataset.theme);
  if (preset) return preset.isDarkPreferred ? 'dark' : 'light';
  return root.classList.contains('dark') ? 'dark' : 'light';
}

/**
 * Reactive theme name, updated via `MutationObserver` whenever
 * `data-theme` or `class` changes on `<html>` (the theme picker writes
 * both synchronously in `applyPreset`).
 *
 * SSR-deterministic initial state: both server and first client render
 * start at `'light'`. The real persisted theme is applied synchronously
 * in `useLayoutEffect` after commit, before the browser paints, so
 * dark-theme users see the right `<img src>` with zero visible flash
 * and React reports no hydration mismatch (hydration only compares the
 * FIRST render).
 */
export function useThemeName(): ThemeName {
  const [themeName, setThemeName] = useState<ThemeName>('light');

  useLayoutEffect(() => {
    if (typeof document === 'undefined') return;
    const root = document.documentElement;
    setThemeName(resolveThemeName());

    const observer = new MutationObserver(() => setThemeName(resolveThemeName()));
    observer.observe(root, { attributes: true, attributeFilter: ['data-theme', 'class'] });
    return () => observer.disconnect();
  }, []);

  return themeName;
}
