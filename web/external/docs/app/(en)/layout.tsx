import { DocsLayout } from 'fumadocs-ui/layouts/docs';
import { DocsProvider } from '@/components/provider';
import { source } from '@/lib/source';
import { baseOptions } from '@/lib/layout.shared';
import '../global.css';

/**
 * Root layout of the EN tree, served under the `/en` prefix
 * (`app/(en)/en/...`).
 */
export default function EnRootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className="flex flex-col min-h-screen">
        <DocsProvider locale="en">
          <DocsLayout tree={source.getPageTree('en')} {...baseOptions('en')}>
            {children}
          </DocsLayout>
        </DocsProvider>
      </body>
    </html>
  );
}
