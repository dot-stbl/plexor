import { DocsLayout } from 'fumadocs-ui/layouts/docs';
import { DocsProvider } from '@/components/provider';
import { source } from '@/lib/source';
import { baseOptions } from '@/lib/layout.shared';
import '../global.css';

/**
 * Root layout of the RU tree (default locale, served at the docs root).
 * A separate root layout exists for EN (`app/(en)/layout.tsx`) so each
 * locale owns its `<html lang>`; Next.js multiple-root-layouts mode means
 * there is no shared `app/layout.tsx`.
 */
export default function RuRootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="ru" suppressHydrationWarning>
      <body className="flex flex-col min-h-screen">
        <DocsProvider locale="ru">
          <DocsLayout tree={source.getPageTree('ru')} {...baseOptions('ru')}>
            {children}
          </DocsLayout>
        </DocsProvider>
      </body>
    </html>
  );
}
