import { i18n } from '@/lib/i18n';
import { uiTranslations } from 'fumadocs-ui/i18n';
import type { BaseLayoutProps } from 'fumadocs-ui/layouts/shared';
import { BrandMark } from '@/components/brand-mark';
import { SidebarFooter } from '@/components/sidebar-footer';

export const docsUrl = 'https://plexor.stbl.space';
export const consoleUrl = 'https://console.plexor.stbl.space';
export const repoUrl = 'https://github.com/dot-stbl/plexor';

/**
 * UI strings for both locales (no official Russian language pack exists in
 * @fumadocs/language yet — only zh-cn/zh-tw — so the handful of visible
 * labels are translated here).
 */
export const translations = i18n
  .translations()
  .extend(uiTranslations())
  .add({
    ru: {
      displayName: 'Русский',
      'Search(search trigger)': 'Поиск',
      'Open Search(search trigger)(aria-label)': 'Открыть поиск',
      'Search(search dialog)': 'Искать документацию…',
      'No results found(search dialog)': 'Ничего не найдено',
      'Choose a language(language switcher)': 'Выбрать язык',
      'Next Page(pagination)': 'Далее',
      'Previous Page(pagination)': 'Назад',
      'On this page(table of contents)': 'На этой странице',
      'No Headings(table of contents)': 'Нет заголовков',
      'Table of Contents(inline table of contents)': 'Содержание',
      'Toggle Theme(theme switcher)(aria-label)': 'Переключить тему',
      'Light(theme switcher)(aria-label)': 'Светлая тема',
      'Dark(theme switcher)(aria-label)': 'Тёмная тема',
      'System(theme switcher)(aria-label)': 'Системная тема',
      'Copy Text(code block)(aria-label)': 'Копировать',
      'Copied Text(code block)(aria-label)': 'Скопировано',
      'Copy Anchor Link(heading anchor)(aria-label)': 'Копировать ссылку на заголовок',
      'Open Sidebar(sidebar)(aria-label)': 'Открыть боковую панель',
      'Close Sidebar(sidebar)(aria-label)': 'Закрыть боковую панель',
      'Show Sidebar(sidebar)': 'Показать боковую панель',
      'Hide Sidebar(sidebar)': 'Скрыть боковую панель',
      'Collapse Sidebar(sidebar)(aria-label)': 'Свернуть боковую панель',
      'Toggle Menu(mobile menu)(aria-label)': 'Меню',
      'Page Not Found(404 page)': 'Страница не найдена',
      'Back to Home(404 page)': 'На главную',
      'The page you are looking for might have been removed, had its name changed, or is temporarily unavailable.(404 page)':
        'Страница, которую вы ищете, могла быть удалена, переименована или временно недоступна.',
      'Last updated on(page footer)': 'Обновлено',
    },
    en: {
      displayName: 'English',
    },
  });

/**
 * Sidebar header — Plexor brand mark + lowercase `plexor` + a small
 * `docs` eyebrow underneath. The default fumadocs header packs everything
 * into one inline span (icon + brand + slash + breadcrumb), which makes
 * the mark look squished at the sidebar's 16-20px scale and bleeds
 * into the search trigger below.
 *
 * This version wraps the mark in its own sized box (size-7) so the SVG
 * has air to breathe, and stacks the brand name + eyebrow as a
 * two-line block with explicit leading. Hover swaps the mark from
 * `text-fd-foreground` to `text-fd-primary` so it reads as a link.
 */
function DocsHeader({ locale }: { locale: string }) {
  const isEn = locale === 'en';
  const home = isEn ? '/en' : '/';
  return (
    <a
      href={home}
      className="docs-brand-link group flex items-center gap-2.5 outline-none"
    >
      <span
        className="docs-brand-mark inline-flex size-7 shrink-0 items-center justify-center text-fd-foreground transition-colors group-hover:text-fd-primary"
        aria-hidden
      >
        <BrandMark />
      </span>
      <span className="flex min-w-0 flex-col leading-tight">
        <span className="truncate text-[15px] font-semibold tracking-tight text-fd-foreground">
          plexor
        </span>
        <span className="truncate text-[11px] font-medium tracking-[0.16em] uppercase text-fd-muted-foreground">
          {isEn ? 'Documentation' : 'Документация'}
        </span>
      </span>
    </a>
  );
}

export function baseOptions(locale: string): BaseLayoutProps {
  return {
    nav: {
      url: locale === 'en' ? '/en' : '/',
    },
    // No `links: [{ text: 'Open console' }]` — that button was a generic
    // SaaS CTA that competed for attention with the actual table of
    // contents. The console link now lives in the page footer (see
    // app/(ru)/layout.tsx / (en)/layout.tsx) so it shows once when
    // readers finish a page, not on every nav render.
    i18n: false,
    themeSwitch: { enabled: false },
    sidebar: {
      navTitle: <DocsHeader locale={locale} />,
      footer: <SidebarFooter key="docs-sidebar-footer" />,
    },
  } as BaseLayoutProps;
}