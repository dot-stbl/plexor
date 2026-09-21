'use client';

import type { ComponentProps, FC, ReactNode } from 'react';
import { useI18n } from 'fumadocs-ui/contexts/i18n';
import { BrandMark } from '@/components/brand-mark';

export interface DocsBrandProps extends ComponentProps<'a'> {
  locale: 'ru' | 'en';
}

/**
 * Plexor mark + lowercase brand + a small uppercase eyebrow underneath
 * ("Документация" / "Documentation"). Renders as an `<a>` and spreads
 * any anchor props the caller forwards (e.g. `className` from a
 * fumadocs slot).
 *
 * Two entry points:
 *
 *   - `<DocsBrand locale={...} />` — use directly when you control
 *     the call site (anchor props spread is up to you).
 *   - `DocsBrandNavTitle` — for the `slots.navTitle` fumadocs slot,
 *     whose contract is `FC<ComponentProps<'a'>>`. Reads the active
 *     locale from `useI18n()` so we don't need one FC per locale;
 *     the RSC boundary serializes this single component reference,
 *     and the locale flows from context client-side. Required
 *     because slots live inside `<SidebarProvider>` (a Client
 *     Component) and a freshly-created closure at render time
 *     crosses the RSC boundary and fails SSG ("Functions cannot be
 *     passed directly to Client Components").
 */
export function DocsBrand({
  locale,
  ...anchorProps
}: DocsBrandProps): ReactNode {
  const isEn = locale === 'en';
  return (
    <a
      {...anchorProps}
      className={`docs-brand-link group flex items-center gap-2.5 rounded-lg px-2 py-1.5 outline-none transition-colors hover:bg-fd-accent focus-visible:ring-2 focus-visible:ring-fd-ring ${anchorProps.className ?? ''}`}
    >
      <span
        className="inline-flex size-9 shrink-0 items-center justify-center rounded-lg bg-fd-secondary text-fd-foreground ring-1 ring-fd-border transition-colors group-hover:text-fd-primary"
        aria-hidden
      >
        <BrandMark className="size-5" />
      </span>
      <span className="flex min-w-0 flex-col leading-none">
        <span className="text-sm font-semibold tracking-tight text-fd-foreground">
          plexor
        </span>
        <span className="mt-1 text-[10px] font-medium tracking-[0.14em] uppercase text-fd-muted-foreground">
          {isEn ? 'Documentation' : 'Документация'}
        </span>
      </span>
    </a>
  );
}

export const DocsBrandNavTitle: FC<ComponentProps<'a'>> = (anchorProps) => {
  const { locale } = useI18n();
  return (
    <DocsBrand {...anchorProps} locale={(locale === 'en' ? 'en' : 'ru')} />
  );
};
DocsBrandNavTitle.displayName = 'DocsBrandNavTitle';
