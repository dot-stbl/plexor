import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';

/**
 * Docs table-of-contents — right-rail scroll-spy that scrapes the
 * rendered article's `h2` and `h3` elements and renders a vertical
 * nav alongside it.
 *
 * Discovery: a `MutationObserver` watches the `<article>` for child
 * additions. The article is identified by its `.docs-prose` class
 * (set on both the TSX `getting-started` page and the MDX route),
 * so the TOC works for both prose paths.
 *
 * Active heading is the one whose `id` matches `window.location.hash`,
 * with a fallback to the heading nearest the top of the viewport.
 * No scrollspy library — `IntersectionObserver` would be more elegant
 * but adds a dep for a v1 with five chapters; a hash-based active
 * state is enough and degrades gracefully when JS is disabled.
 */
interface Heading {
  readonly id: string;
  readonly text: string;
  readonly level: 2 | 3;
}

const HEADING_SELECTOR = 'article.docs-prose h2, article.docs-prose h3';

export function DocsToc(): ReactNode {
  const [headings, setHeadings] = useState<readonly Heading[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);

  useEffect(() => {
    const article = document.querySelector<HTMLElement>('article.docs-prose');
    if (!article) return;

    const scrape = () => {
      const next: Heading[] = [];
      for (const element of article.querySelectorAll<HTMLElement>(HEADING_SELECTOR)) {
        const id = element.id;
        if (!id) continue;
        const level = element.tagName === 'H3' ? 3 : 2;
        next.push({ id, text: element.textContent ?? '', level });
      }
      setHeadings(next);
    };

    scrape();
    const observer = new MutationObserver(scrape);
    observer.observe(article, { childList: true, subtree: true });
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (headings.length === 0) return;

    const updateFromHash = () => {
      const hash = window.location.hash.slice(1);
      if (hash && headings.some((heading) => heading.id === hash)) {
        setActiveId(hash);
      }
    };

    updateFromHash();
    window.addEventListener('hashchange', updateFromHash);
    return () => window.removeEventListener('hashchange', updateFromHash);
  }, [headings]);

  if (headings.length === 0) {
    return <aside className="hidden w-56 shrink-0 lg:block" aria-hidden="true" />;
  }

  return (
    <aside className="hidden w-56 shrink-0 lg:block">
      <nav aria-label="On this page" className="sticky top-20">
        <div className="mb-3 font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
          On this page
        </div>
        <ul className="space-y-1.5 border-l border-border/60 text-sm">
          {headings.map((heading) => {
            const active = heading.id === activeId;
            return (
              <li
                key={heading.id}
                className={heading.level === 3 ? 'pl-6' : 'pl-3'}
              >
                <a
                  href={`#${heading.id}`}
                  className={`-ml-px block border-l py-0.5 pl-3 transition-colors duration-fast ease-out ${
                    active
                      ? 'border-foreground font-medium text-foreground'
                      : 'border-transparent text-muted-2 hover:text-foreground'
                  }`}
                >
                  {heading.text}
                </a>
              </li>
            );
          })}
        </ul>
      </nav>
    </aside>
  );
}