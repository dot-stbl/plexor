/**
 * Marketing footer — three outbound links + a copyright / version chip
 * on the right. The footer is intentionally thin: a single row with
 * a hairline separator, no widgets, no newsletter signup. The Plexor
 * brand lives in the header above; the footer is scope, not chrome.
 *
 * Links are operator-oriented:
 *   - "Source" → the repo (operators who self-host Plexor may want to
 *     read the code; no claim it is a contribution gate)
 *   - "Community" → discussions + support (replaces "Issues", which
 *     framed the project as a development tracker)
 *   - "Console" → the operator-installed app, when running
 */
export function MarketingFooter() {
  return (
    <footer className="border-t border-border">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-6 py-6 text-xs text-muted-2">
        <div className="flex flex-wrap items-center gap-4">
          <a
            href="https://github.com/dot-stbl/plexor"
            className="transition-colors duration-fast ease-out hover:text-foreground"
          >
            Source
          </a>
          <a
            href="https://github.com/dot-stbl/plexor/discussions"
            className="transition-colors duration-fast ease-out hover:text-foreground"
          >
            Community
          </a>
          <a
            href="https://plexor.stbl.space"
            className="transition-colors duration-fast ease-out hover:text-foreground"
          >
            Console
          </a>
        </div>
        <span className="font-mono">plexor v0.2 pre-stable</span>
      </div>
    </footer>
  );
}
