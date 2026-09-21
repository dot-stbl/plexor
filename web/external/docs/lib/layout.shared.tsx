import { i18n } from '@/lib/i18n';
import { uiTranslations } from 'fumadocs-ui/i18n';
import type { BaseLayoutProps } from 'fumadocs-ui/layouts/shared';
import { BrandMark } from '@/components/brand-mark';
import { SidebarFooter } from '@/components/sidebar-footer';

export const docsUrl = 'https://plexor.stbl.space';
export const consoleUrl = 'https://console.plexor.stbl.space';

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

export function baseOptions(locale: string): BaseLayoutProps {
  return {
    nav: {
      title: (
        <span className="inline-flex items-center gap-2 font-medium tracking-tight">
          <BrandMark />
          plexor
          <span className="text-fd-muted-foreground font-normal">/ docs</span>
        </span>
      ),
      url: locale === 'en' ? '/en' : '/',
    },
    links: [
      {
        text: locale === 'en' ? 'Open console' : 'Открыть консоль',
        url: consoleUrl,
        external: true,
      },
    ],
    i18n: false,
    themeSwitch: { enabled: false },
    sidebar: {
      footer: <SidebarFooter key="docs-sidebar-footer" />,
    },
  } as BaseLayoutProps;
}