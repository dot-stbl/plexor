import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';

import en from './locales/en/common.json';
import ru from './locales/ru/common.json';

export const SUPPORTED_LANGUAGES = ['en', 'ru'] as const;
export type Language = (typeof SUPPORTED_LANGUAGES)[number];
export const DEFAULT_LANGUAGE: Language = 'en';

/**
 * i18n setup. react-i18next + browser language detector.
 *
 * Resources are bundled (no lazy loading yet — catalog is small, ~200 keys).
 * Language is detected from localStorage ('plexor-lang') → navigator.language,
 * with fallback to DEFAULT_LANGUAGE ('en').
 *
 * PreferencesProvider syncs the language pref; this module is initialized once
 * at app bootstrap in main.tsx.
 */
void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      en: { translation: en },
      ru: { translation: ru },
    },
    fallbackLng: DEFAULT_LANGUAGE,
    supportedLngs: [...SUPPORTED_LANGUAGES],
    interpolation: {
      escapeValue: false,
    },
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: 'plexor-lang',
      caches: ['localStorage'],
    },
  });

export default i18n;
