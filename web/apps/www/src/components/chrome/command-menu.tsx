import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { Search } from '@nine-thirty-five/material-symbols-react/rounded/700';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Button } from '@/components/ui/button';
import { SEARCH_INDEX } from '@/content/search-index';
import { useCommandMenu } from './command-menu-store';
import { filterSearchEntries, groupBySection } from './command-menu-filter';

/**
 * Global `⌘K` / `Ctrl K` search dialog (spec §4.3). Mounted once in
 * `__root.tsx` so it works identically on `/` and every `/docs/*` page.
 * Open state lives in `CommandMenuProvider` (also mounted in
 * `__root.tsx`) so the header's search-trigger button can open it too.
 */
export function CommandMenu() {
  const { open, setOpen } = useCommandMenu();
  const [query, setQuery] = useState('');
  const navigate = useNavigate();

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) {
        event.preventDefault();
        setOpen(!open);
      }
    }
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [open, setOpen]);

  useEffect(() => {
    if (!open) setQuery('');
  }, [open]);

  const filtered = useMemo(() => filterSearchEntries(SEARCH_INDEX, query), [query]);
  const grouped = useMemo(() => groupBySection(filtered), [filtered]);

  const handleSelect = useCallback(
    (url: string) => {
      setOpen(false);
      void navigate({ to: url });
    },
    [navigate, setOpen],
  );

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogContent className="top-24 max-h-[70vh] translate-y-0 gap-3 sm:max-w-lg" showCloseButton={false}>
        <DialogTitle className="sr-only">Search documentation</DialogTitle>
        <DialogDescription className="sr-only">
          Search the Plexor docs by page title or chapter.
        </DialogDescription>
        <div className="flex items-center gap-2 border-b border-border pb-2">
          <Search className="size-4 shrink-0 text-muted-2" />
          <Input
            autoFocus
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search docs…"
            aria-label="Search documentation"
            className="h-8 border-none bg-transparent px-0 shadow-none focus-visible:ring-0"
          />
        </div>
        <ScrollArea className="max-h-72">
          <div className="flex flex-col gap-3 py-1 pr-2">
            {grouped.size === 0 ? (
              <p className="px-2 py-6 text-center text-xs text-muted-2">No results.</p>
            ) : (
              Array.from(grouped.entries()).map(([section, items]) => (
                <div key={section}>
                  <div className="px-2 pb-1 font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
                    {section}
                  </div>
                  <div className="flex flex-col gap-0.5">
                    {items.map((item) => (
                      <Button
                        key={item.url}
                        variant="ghost"
                        onClick={() => handleSelect(item.url)}
                        className="h-auto w-full justify-start px-2 py-1.5 text-left text-sm font-normal"
                      >
                        {item.title}
                      </Button>
                    ))}
                  </div>
                </div>
              ))
            )}
          </div>
        </ScrollArea>
      </DialogContent>
    </Dialog>
  );
}
