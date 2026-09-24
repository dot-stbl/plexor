import { Link } from '@tanstack/react-router';
import type { ReactNode } from 'react';
import { KeyboardArrowRight } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { cn } from '@/lib/utils';
import type { DocsChapter } from './docs-chapters';

/**
 * One chapter row in `DocsSidebar` (`./docs-sidebar`): a `Link` to the
 * chapter's own page, plus — for chapters with more than one page — a
 * `Collapsible` trigger that expands/collapses the sub-page list. Backed by
 * the real `Collapsible`/`CollapsibleTrigger`/`CollapsibleContent`
 * primitive (`@/components/ui/collapsible`, react-aria-components) instead
 * of a hand-rolled `aria-expanded` button. Same visual density as before;
 * open/closed state is still owned by the parent `DocsSidebar` (controlled)
 * so auto-expand-active-chapter and cross-render persistence keep working.
 */
export interface ChapterLinkProps {
  readonly chapter: DocsChapter;
  readonly num: string;
  readonly pathname: string;
  readonly active: boolean;
  readonly expanded: boolean;
  readonly onExpandedChange: (next: boolean) => void;
}

export function ChapterLink({
  chapter,
  num,
  pathname,
  active,
  expanded,
  onExpandedChange,
}: ChapterLinkProps): ReactNode {
  const hasPages = chapter.pages.length > 1;
  const labelClass = active
    ? 'bg-muted font-medium text-foreground'
    : 'text-muted-2 hover:bg-muted/60 hover:text-foreground';

  const label = (
    <Link
      to={chapter.slug}
      className={cn(
        'flex flex-1 items-center gap-2 rounded-md px-2 py-1.5 text-sm transition-colors duration-fast ease-out',
        labelClass,
      )}
      aria-current={active ? 'page' : undefined}
    >
      <span className="font-mono text-[10px] tracking-[0.12em] text-muted-2/80">{num}</span>
      <span>{chapter.label}</span>
    </Link>
  );

  if (!hasPages) {
    return label;
  }

  return (
    <Collapsible open={expanded} onOpenChange={onExpandedChange}>
      <div className="flex items-center gap-1">
        {label}
        <CollapsibleTrigger
          aria-label={expanded ? 'Collapse chapter' : 'Expand chapter'}
          className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md text-muted-2/70 transition-colors duration-fast ease-out hover:text-foreground"
        >
          <KeyboardArrowRight
            aria-hidden="true"
            className={cn(
              'size-3.5 transition-transform duration-fast ease-out',
              expanded && 'rotate-90',
            )}
          />
        </CollapsibleTrigger>
      </div>
      <CollapsibleContent>
        <ul className="ml-6 mt-0.5 space-y-0.5 border-l border-border/60 pl-3">
          {chapter.pages.map((page) => (
            <li key={page.slug}>
              {page.soon ? (
                <div className="flex items-center justify-between gap-2">
                  <span className="rounded-md px-2 py-1 text-[13px] text-muted-2/60">
                    {page.title}
                  </span>
                  <span className="font-mono text-[10px] uppercase tracking-[0.12em] text-muted-2/40">
                    soon
                  </span>
                </div>
              ) : (
                <Link
                  to={page.slug}
                  aria-current={pathname === page.slug ? 'page' : undefined}
                  className={cn(
                    'block rounded-md px-2 py-1 text-[13px] transition-colors duration-fast ease-out',
                    pathname === page.slug
                      ? 'bg-muted font-medium text-foreground'
                      : 'text-muted-2 hover:text-foreground',
                  )}
                >
                  {page.title}
                </Link>
              )}
            </li>
          ))}
        </ul>
      </CollapsibleContent>
    </Collapsible>
  );
}
