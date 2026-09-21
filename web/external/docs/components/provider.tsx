'use client';

import { RootProvider } from 'fumadocs-ui/provider/next';
import { i18nProvider } from 'fumadocs-ui/i18n';
import type { ReactNode } from 'react';
import StaticSearchDialog from '@/components/search-dialog';
import { translations } from '@/lib/layout.shared';

/**
 * Locale switch for a static export with a hidden default locale: RU lives
 * at the root (`/page/`), EN under `/en/page/`. The default fumadocs
 * onChange blindly prefixes the locale (`/ru/page/` — a 404 here), so we
 * re-map the path ourselves: strip any `/en` prefix, then prepend the new
 * locale's prefix.
 */
function switchLocale(next: string): void {
  const path = window.location.pathname;
  const stripped = path.replace(/^\/en(\/|$)/, '/');
  const prefix = next === 'en' ? '/en' : '';
  const target = `${prefix}${stripped === '/' ? '/' : stripped}`.replace(/\/{2,}/g, '/');

  window.location.assign(target);
}

export function DocsProvider({ locale, children }: { locale: string; children: ReactNode }) {
  return (
    <RootProvider
      i18n={{
        ...i18nProvider(translations, locale),
        onLocaleChange: switchLocale,
      }}
      search={{ SearchDialog: StaticSearchDialog }}
    >
      {children}
    </RootProvider>
  );
}
