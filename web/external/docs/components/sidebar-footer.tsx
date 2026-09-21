'use client';

import { Languages } from 'lucide-react';
import { LanguageSelect, LanguageSelectText } from 'fumadocs-ui/layouts/shared/slots/language-select';
import { ThemeSwitch } from 'fumadocs-ui/layouts/shared/slots/theme-switch';

/**
 * Compact single-row sidebar footer: language select + theme switch side by
 * side, replacing the default fumadocs footer that stacks them as two
 * full-width rows eating the sidebar bottom.
 */
export function SidebarFooter() {
  return (
    // no padding here: fumadocs wraps the footer in a p-4 pt-2 container
    <div className="flex flex-row items-center gap-2">
      <LanguageSelect
        variant="secondary"
        className="flex-1 text-fd-muted-foreground text-start justify-start bg-fd-secondary/50"
      >
        <Languages className="size-4" aria-hidden />
        <LanguageSelectText />
      </LanguageSelect>
      {/* docs-theme-square: rounded-md override — the pill-shaped default
          (rounded-full) reads uneven next to the rectangular language
          select; see global.css */}
      <ThemeSwitch mode="light-dark" className="docs-theme-square" />
    </div>
  );
}
