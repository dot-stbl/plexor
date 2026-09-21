import { i18n } from '@/lib/i18n';
import { uiTranslations } from 'fumadocs-ui/i18n';
import type { DocsLayoutProps } from 'fumadocs-ui/layouts/docs';
import { DocsBrandNavTitle } from '@/components/sidebar/brand';
import { SidebarFooter } from '@/components/sidebar/footer';
import { SidebarOverrides } from '@/components/sidebar/tree-overrides';

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
 * Returns the layout config for one of the two locale trees. The
 * return type is `Omit<DocsLayoutProps, 'tree'>` — `tree` is supplied
 * by each locale's `app/(ru)/layout.tsx` and `app/(en)/layout.tsx`
 * (the source tree differs per locale), but `sidebar` lives in
 * `DocsLayoutProps`, not `BaseLayoutProps`, so it has to come back
 * here. `BaseLayoutProps` only owns `nav`, `links`, `slots`,
 * `themeSwitch`, `searchToggle`, `i18n`.
 *
 * The Plexor brand block lives in `slots.navTitle` (topmost slot of
 * the sidebar header row, before the search trigger) — passing it
 * there replaces fumadocs' default `InlineNavTitle` breadcrumb with
 * the brand mark + lowercase wordmark + locale eyebrow. The locale
 * is read from `useI18n()` inside the component so we can hand
 * fumadocs a single client-component reference instead of a
 * freshly-allocated closure (which would fail SSG when crossing the
 * RSC boundary into the Client Component sidebar).
 *
 * Default language/theme slots are disabled (`i18n: false`,
 * `themeSwitch: { enabled: false }`) so the bottom pill-pair footer
 * is the only one the user sees.
 */
export function baseOptions(locale: string): Omit<DocsLayoutProps, 'tree'> {
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
    slots: {
      // Slot contract: `FC<ComponentProps<'a'>>` — fumadocs calls it
      // with `{ className }` (and nothing else). A single client
      // component reference is required (functions can't cross the
      // RSC boundary). The locale eyebrow ("Документация" /
      // "Documentation") is read from the i18n context inside the
      // component, not from this call site.
      navTitle: DocsBrandNavTitle,
    },
    sidebar: {
      footer: <SidebarFooter />,
      components: SidebarOverrides,
      collapsible: true,
      defaultOpenLevel: 1,
    },
  };
}