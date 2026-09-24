/**
 * Подготовка страницы к детерминированному рендеру — вьюпорты, фейковая
 * сессия (для page-режима), отключение анимаций, ожидание скелетонов.
 * Общий код между `page-target.ts` и `story-target.ts`.
 */

import type { Page } from 'playwright';

export type Theme = 'light' | 'dark';

export const DESKTOP_VIEWPORT = { width: 1280, height: 800 } as const;
export const MOBILE_VIEWPORT = { width: 390, height: 844 } as const;

export function viewportFor(mobile: boolean): { width: number; height: number } {
  return mobile ? MOBILE_VIEWPORT : DESKTOP_VIEWPORT;
}

/**
 * Форма `StoredSession` из `src/shared/lib/session.ts` —
 * продублирована здесь (не импортируется), т.к. scripts/ живёт вне
 * tsconfig `include` приложения и не должен тянуть runtime-код src/ в
 * агентский CLI.
 */
interface FakeStoredSession {
  readonly accessToken: string;
  readonly refreshToken: string;
  readonly expiresAt: number;
  readonly user: {
    readonly id: string;
    readonly email: string;
    readonly displayName: string;
    readonly roles: readonly string[];
  };
}

const FAKE_USER_ID = 'agent-shot-admin';

function fakeSession(): FakeStoredSession {
  return {
    accessToken: 'agent-shot-fake-access-token',
    refreshToken: 'agent-shot-fake-refresh-token',
    expiresAt: Date.now() + 1000 * 60 * 60 * 24 * 365, // на год вперёд
    user: {
      id: FAKE_USER_ID,
      email: 'agent@plexor.local',
      displayName: 'Agent Shot',
      roles: ['admin'],
    },
  };
}

/**
 * Сеет `plexor-auth` (валидная фейковая сессия) и `plexor-preferences`
 * (тема) в localStorage ДО первого скрипта страницы — так роутовый
 * `beforeLoad` (см. `src/routes/index.tsx`) видит валидную сессию, а
 * `PreferencesProvider` — нужную тему сразу при монтировании. Пишем и
 * per-user, и base-ключ (`preferencesStorageKey` в
 * `preferences-provider.tsx` предпочитает per-user, если он есть).
 */
export async function seedAuthAndPreferences(page: Page, theme: Theme): Promise<void> {
  await page.addInitScript(
    ({ session, prefsJson, userId }: { session: FakeStoredSession; prefsJson: string; userId: string }) => {
      try {
        window.localStorage.setItem('plexor-auth', JSON.stringify(session));
        window.localStorage.setItem('plexor-preferences', prefsJson);
        window.localStorage.setItem(`plexor-preferences::${userId}`, prefsJson);
      } catch {
        // localStorage недоступен (приватный режим и т.п.) — страница
        // упадёт на анонимный вид, это законный FAIL для отчёта.
      }
    },
    { session: fakeSession(), prefsJson: JSON.stringify({ theme }), userId: FAKE_USER_ID },
  );
}

/**
 * CSS, убивающий анимации/переходы — копия правила из
 * `.storybook/test-runner.ts` (preVisit), чтобы скриншоты были
 * детерминированными. Требует существующего документа — звать после
 * навигации.
 */
export async function disableAnimations(page: Page): Promise<void> {
  await page.addStyleTag({
    content: `
      *, *::before, *::after {
        animation-duration: 0s !important;
        animation-delay: 0s !important;
        animation-iteration-count: 1 !important;
        transition-duration: 0s !important;
        transition-delay: 0s !important;
      }
    `,
  });
}

/** Ждёт исчезновения скелетонов (максимум maxMs), но не проваливает вызов. */
export async function waitForSkeletonsGone(page: Page, maxMs = 5_000): Promise<void> {
  try {
    await page.waitForFunction(
      () => document.querySelectorAll('[data-slot="skeleton"]').length === 0,
      undefined,
      { timeout: maxMs },
    );
  } catch {
    // Скелетон(ы) не исчезли за maxMs — не блокируем скриншот, отчёт
    // покажет их состояние как есть.
  }
}

/** Ждёт network idle, но не проваливает вызов при таймауте (SPA держит
 *  открытые соединения — MSW/xterm/websocket-подобные, это нормально). */
export async function waitForNetworkIdleBestEffort(page: Page, maxMs = 8_000): Promise<void> {
  try {
    await page.waitForLoadState('networkidle', { timeout: maxMs });
  } catch {
    // не критично — продолжаем с тем, что уже отрендерилось
  }
}
