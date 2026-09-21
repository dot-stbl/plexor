'use client';

import { LanguageSelect, LanguageSelectText } from 'fumadocs-ui/layouts/shared/slots/language-select';
import { ThemeSwitch } from 'fumadocs-ui/layouts/shared/slots/theme-switch';
import { Language } from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Compact sidebar footer — language select + theme switch in one row.
 * The language pill uses the Material Symbols `Language` glyph (matches
 * the rest of Plexor's icon system); the theme switch overrides the
 * default rounded-full shape with `docs-theme-square` (defined in
 * global.css) so the two controls sit flush.
 *
 * No "Open console" link here — that lives in the page footer instead of
 * the sidebar, so reading flow doesn't push operators out of the docs.
 */
export function SidebarFooter() {
  return (
    <div className="flex flex-row items-center gap-2">
      <LanguageSelect
        variant="secondary"
        className="flex-1 text-fd-muted-foreground justify-start bg-fd-secondary/50 text-start"
      >
        <Language className="size-4" aria-hidden />
        <LanguageSelectText />
      </LanguageSelect>
      <ThemeSwitch mode="light-dark" className="docs-theme-square shrink-0" />
    </div>
  );
}