import '@tanstack/react-router';

/**
 * Типизируем `staticData` роутов. Каждый роут может объявить `crumb` — из него
 * `AppHeader` строит хлебные крошки через `useMatches()` (крошки живут ТОЛЬКО
 * в верхнем баре, не в страницах — см. web-frontend.md rule 57).
 */
declare module '@tanstack/react-router' {
  interface StaticDataRouteOption {
    crumb?: string;
  }
}

export {};
