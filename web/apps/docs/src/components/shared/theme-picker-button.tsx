import { useCallback } from 'react';
import { applyPreset, presets } from '@plexor/ui/themes';

/**
 * Tiny theme-cycle button. Cycles through the built-in presets in
 * order: plexor-default-light → plexor-default-dark → plexor-noir →
 * plexor-default-light. Persists via `localStorage` (same key the
 * inline boot script reads) and writes `data-theme` on `<html>` so
 * the rest of the app reads the right tokens immediately.
 *
 * Both marketing and docs chromes mount this same component — the
 * label is supplied by the parent via `className`, not hard-coded
 * here, because the marketing header shows "Тема" while the docs
 * header shows "Theme".
 *
 * No exposed state: the button is stateless beyond the imperative
 * cycle. The next paint reads the data-theme attribute directly, so
 * any selector that depends on it (e.g. `[data-theme="plexor-noir"]`
 * when we add per-preset rules later) re-resolves without React
 * involvement.
 */
const STORAGE_KEY = 'plexor-theme';

export function ThemePickerButton({ className }: { className?: string }) {
  const cycle = useCallback(() => {
    const root = document.documentElement;
    const currentId = root.dataset.theme ?? presets[0]?.id ?? '';
    const currentIndex = presets.findIndex((preset) => preset.id === currentId);
    const nextIndex = (currentIndex + 1) % presets.length;
    const next = presets[nextIndex] ?? presets[0]!;
    applyPreset(next);
    try {
      window.localStorage.setItem(STORAGE_KEY, next.id);
    } catch {
      // localStorage unavailable — preset still applies for this session
    }
  }, []);

  return (
    <button type="button" onClick={cycle} className={className}>
      theme
    </button>
  );
}