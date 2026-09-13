import type { ThemePreset } from './presets';

/** The CSS variable prefix every themed token is written under. */
const TOKEN_PREFIX = '--';

/**
 * Apply a preset's tokens to `document.documentElement` as CSS custom
 * properties. Also sets `data-theme="<preset.id>"` so the `[data-theme="..."]`
 * selectors we will introduce later can scope rules per preset, and toggles
 * the `.dark` Tailwind class to match `isDarkPreferred` so utilities like
 * `dark:bg-...` resolve correctly.
 *
 * Calling this twice is cheap: each call overwrites every themed token with
 * the preset's values, and CSS custom properties set on the root element
 * take precedence over the `:root` / `.dark` declarations in `index.css`.
 */
export function applyPreset(preset: ThemePreset): void {
  if (typeof document === 'undefined') return;
  const root = document.documentElement;

  for (const [token, value] of Object.entries(preset.tokens)) {
    root.style.setProperty(`${TOKEN_PREFIX}${token}`, value);
  }

  root.dataset.theme = preset.id;
  root.classList.toggle('dark', preset.isDarkPreferred);
}
