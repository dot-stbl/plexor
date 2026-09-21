'use client';

import { LanguageSelect, LanguageSelectText } from 'fumadocs-ui/layouts/shared/slots/language-select';
import { ThemeSwitch } from 'fumadocs-ui/layouts/shared/slots/theme-switch';
import { Language } from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Sidebar footer — language select + theme switch in one pill-pair.
 * The two controls share a thin `border-fd-border` wrapper with internal
 * padding so they read as a single chip; the theme switch overrides the
 * default `rounded-full` shape via `docs-theme-square` (defined in
 * global.css) so its `border-radius: 0.375rem !important` matches the
 * surrounding wrapper.
 *
 * Why this lives here and not in `slots.languageSelect` / `slots.themeSwitch`:
 * the default slot render in `fumadocs-ui/layouts/docs/slots/sidebar.js`
 * puts them in the same row as `iconLinks` (GitHub icon etc) in the
 * sidebar TOP section. We want them together at the BOTTOM of the
 * sidebar, so we disable the default slots via `i18n: false` and
 * `themeSwitch: { enabled: false }` in `baseOptions()` and render them
 * here as the sidebar `footer` slot.
 *
 * No "Open console" link here — that lives in the page footer (see
 * `app/(ru)/layout.tsx` / `app/(en)/layout.tsx`) so reading flow
 * doesn't push operators out of the docs mid-article.
 */
export function SidebarFooter() {
  return (
    <div className="rounded-lg border border-fd-border bg-fd-secondary/40 p-0.5 flex items-center gap-0.5">
      <LanguageSelect
        variant="secondary"
        className="flex-1 rounded-md bg-transparent text-fd-muted-foreground hover:text-fd-foreground hover:bg-fd-background/60 justify-start text-start"
      >
        <Language className="size-4" aria-hidden />
        <LanguageSelectText />
      </LanguageSelect>
      <ThemeSwitch
        mode="light-dark"
        className="docs-theme-square size-8 shrink-0 rounded-md bg-transparent hover:bg-fd-background/60"
      />
    </div>
  );
}