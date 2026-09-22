import { useCallback, useEffect, useState } from 'react';
import { LightMode } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { applyPreset, presets, type ThemePreset } from '@plexor/ui/themes';

/**
 * Tiny theme-cycle button. Cycles through the built-in presets in
 * order: plexor-default-light → plexor-default-dark → plexor-noir →
 * plexor-default-light. Persists via `localStorage` (same key the
 * inline boot script reads) and writes `data-theme` on `<html>` so
 * the rest of the app reads the right tokens immediately.
 *
 * Renders a single Material Symbols `LightMode` glyph at 16px. The
 * icon is the same on every chrome — there is no per-parent copy.
 * Parents style the surrounding button (color, hover, layout) via
 * `className`; the icon's `size-4` className handles glyph dimensions.
 *
 * The aria-label mirrors the active preset so screen readers know
 * what the click will toggle away from. The label updates on mount
 * and after each cycle by listening to `data-theme` on `<html>`.
 *
 * No exposed state: the button is stateless beyond the imperative
 * cycle. The next paint reads the data-theme attribute directly, so
 * any selector that depends on it (e.g. `[data-theme="plexor-noir"]`
 * when we add per-preset rules later) re-resolves without React
 * involvement.
 */
const STORAGE_KEY = 'plexor-theme';

export function ThemePickerButton({ className }: { className?: string }) {
  const [active, setActive] = useState<ThemePreset | null>(null);

  useEffect(() => {
    const read = () => {
      const id = document.documentElement.dataset.theme ?? '';
      const match = presets.find((preset) => preset.id === id) ?? null;
      setActive(match);
    };
    read();
    const observer = new MutationObserver(read);
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['data-theme'],
    });
    return () => observer.disconnect();
  }, []);

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

  const label = active ? `Theme: ${active.id} — click to switch` : 'Switch theme';

  return (
    <button
      type="button"
      onClick={cycle}
      aria-label={label}
      title={label}
      className={className}
    >
      <LightMode className="size-4" />
    </button>
  );
}
