import { useEffect, useState } from 'react';
import { cn } from '@/lib/utils';

const STORAGE_KEY = 'plexor-theme';
type Theme = 'light' | 'dark';

/**
 * ThemeToggle — light/dark switch persisted to localStorage.
 * Mirrors the topnav sun/moon toggle in design-system/index.html.
 * Initial theme is applied by the inline script in main.tsx (no flash).
 */
export interface ThemeToggleProps {
  className?: string;
}

export function ThemeToggle({ className }: ThemeToggleProps) {
  const [theme, setTheme] = useState<Theme>('light');

  useEffect(() => {
    const current = document.documentElement.classList.contains('dark') ? 'dark' : 'light';
    setTheme(current);
  }, []);

  function flip() {
    const next: Theme = theme === 'dark' ? 'light' : 'dark';
    setTheme(next);
    document.documentElement.classList.toggle('dark', next === 'dark');
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch (_) {
      // ignore
    }
  }

  return (
    <button
      type="button"
      onClick={flip}
      aria-pressed={theme === 'dark'}
      aria-label={`Switch to ${theme === 'dark' ? 'light' : 'dark'} theme`}
      data-theme-toggle
      data-od-id="theme-toggle"
      className={cn(
        'icon-btn',
        'data-[theme-toggle]:inline-flex data-[theme-toggle]:items-center data-[theme-toggle]:gap-2',
        className,
      )}
      style={{ width: 'auto', paddingLeft: 8, paddingRight: 8, gap: 8 }}
    >
      {/* Moon icon — visible in light (offering dark mode) */}
      <svg
        viewBox="0 0 16 16"
        fill="none"
        aria-hidden
        style={{ width: 14, height: 14 }}
      >
        <path
          d="M13.2 9.4A5.5 5.5 0 0 1 6.6 2.8 5.5 5.5 0 1 0 13.2 9.4Z"
          stroke="currentColor"
          strokeWidth="1.3"
          strokeLinejoin="round"
        />
      </svg>
      <span data-theme-label>{theme === 'dark' ? 'Тёмная' : 'Светлая'}</span>
    </button>
  );
}
