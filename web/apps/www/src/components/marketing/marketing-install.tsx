import { Link } from '@tanstack/react-router';
import { motion } from 'motion/react';
import { CopyButton } from '@/components/ui/copy-button';
import { useReducedMotionSafe } from '@/components/motion';
import { COMMAND_LINES, OUTPUT_LINES, TERMINAL_LINES, copyableLines } from './marketing-install-lines';
import { typedLength, typedLinesUpTo } from './timed-reveal';
import { useElapsedMs } from './use-elapsed-ms';
import { useInViewOnce } from './use-in-view-once';

const MS_PER_CHAR = 22;
const FULL_TEXT = COMMAND_LINES.join('\n');
const TYPING_DURATION_MS = FULL_TEXT.length * MS_PER_CHAR;

/**
 * Quickstart terminal block (spec §3.2) — the full clone + run path. The
 * hero (`marketing-hero.tsx`) already carries a one-line copyable clone
 * command; this section is the complete picture. Once in view, the two
 * commands type themselves in (brief: "types commands ... when in
 * view"), then 2 honest output lines fade in — see
 * `marketing-install-lines.ts` for why those exact two lines and nothing
 * fabricated (no invented port, no timing claim). The copy button copies
 * the real commands regardless of typing progress — it's wired to the
 * static `TERMINAL_LINES` data, not the animated state.
 *
 * Reduced motion: renders every line already typed and the output
 * already visible, no cursor.
 *
 * Two-column at `lg:` (text left, terminal right) — this panel used to
 * sit paired 50/50 with the "how it runs" screenshot panel (`PanelRow`),
 * which kept its own `max-w-2xl` terminal from ever needing more room.
 * Now that it's a full-width panel on its own (screenshot legibility
 * fix, `routes/(marketing)/index.tsx`), a single `max-w-2xl` column on
 * an otherwise-empty wide black panel left a large dead area to its
 * right; splitting text/terminal into two columns uses that width
 * instead of leaving it blank.
 */
export function MarketingInstall() {
  const copyValue = copyableLines(TERMINAL_LINES);
  const [ref, inView] = useInViewOnce<HTMLDivElement>();
  const reducedMotion = useReducedMotionSafe();

  const elapsed = useElapsedMs(inView && !reducedMotion, TYPING_DURATION_MS);
  const visibleChars = reducedMotion ? FULL_TEXT.length : typedLength(FULL_TEXT.length, elapsed, MS_PER_CHAR);
  const typedLines = typedLinesUpTo(COMMAND_LINES, visibleChars);
  const typingDone = reducedMotion || visibleChars >= FULL_TEXT.length;
  const cursorLineIndex = typedLines.length - 1;

  return (
    <div className="lg:grid lg:grid-cols-[2fr_3fr] lg:items-start lg:gap-12">
      <div>
        <p className="mb-3 text-sm font-medium text-background/50">Get running</p>
        <h2 className="text-3xl font-extrabold tracking-tight text-background">
          One binary. One process. Running in minutes.
        </h2>
        <p className="mt-4 text-sm leading-6 text-background/70">
          There is no packaged install command yet.{' '}
          <Link
            to="/docs/getting-started/install"
            className="text-background underline underline-offset-4 hover:no-underline"
          >
            Read the full install guide
          </Link>{' '}
          for airgapped, CLI and ISO paths.
        </p>
      </div>

      <div ref={ref} className="mt-8 overflow-hidden rounded-2xl border border-border bg-card lg:mt-0">
        <div className="flex items-center justify-between border-b border-border px-4 py-2">
          <span className="font-mono text-xs text-muted-2">terminal</span>
          <CopyButton value={copyValue} copyLabel="Copy the install commands" />
        </div>
        <div className="overflow-x-auto px-4 py-4 font-mono text-sm leading-6 tabular-nums">
          {COMMAND_LINES.map((line, index) => {
            const shown = typedLines[index];
            if (shown === undefined) return null;
            return (
              <div key={line} className="text-foreground">
                {shown}
                {!typingDone && !reducedMotion && index === cursorLineIndex && (
                  <span className="animate-pulse">▍</span>
                )}
              </div>
            );
          })}
          {reducedMotion ? (
            <div className="mt-2 space-y-0.5">
              {OUTPUT_LINES.map((line) => (
                <div key={line} className="text-muted-2">
                  {line}
                </div>
              ))}
            </div>
          ) : (
            <motion.div
              className="mt-2 space-y-0.5"
              initial={{ opacity: 0 }}
              animate={{ opacity: typingDone ? 1 : 0 }}
              transition={{ duration: 0.4 }}
            >
              {OUTPUT_LINES.map((line) => (
                <div key={line} className="text-muted-2">
                  {line}
                </div>
              ))}
            </motion.div>
          )}
        </div>
      </div>
    </div>
  );
}
