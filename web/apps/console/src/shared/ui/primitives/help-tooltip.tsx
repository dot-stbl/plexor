import type { ReactNode } from 'react';
import { Tooltip, TooltipTrigger } from 'react-aria-components';
import { Help } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { TooltipContent } from '@/shared/ui/primitives/tooltip';
import { cn } from '@/lib/utils';

/**
 * HelpTooltip — `?`-кружок рядом с label, раскрывает подсказку по hover/focus.
 * Приём из YC (help на каждом сложном поле). Триггер — button (фокусируемый,
 * a11y), не внутри `<label>` — чтобы не активировать контрол.
 *
 * Implementation note: this used to route through the Plexor Tooltip compat
 * shim (base-ui era: `<TooltipProvider><Tooltip><TooltipTrigger render={...}>`).
 * The shim wrapped `<RACTooltipTrigger>` but its TooltipProvider was a
 * dead context, so the popup never actually rendered on hover — the `?`
 * icon was visible but the tooltip never appeared. We now use react-aria-
 * components primitives directly and keep our shim's TooltipContent (for
 * the side/align/placement/slide classes it owns). Any other caller that
 * still uses the compat shim gets the icon (via the children-passthrough fix)
 * but the popup is also dead there — same root cause. Audit the other
 * shim consumers; if they need the popup, rewrite them similarly.
 */
export function HelpTooltip({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <TooltipTrigger delay={200}>
      <button
        type="button"
        aria-label="Help"
        className={cn(
          'inline-flex items-center text-muted-foreground/70 outline-none transition-colors hover:text-muted-foreground focus-visible:text-foreground',
          className,
        )}
      >
        <Help className="size-3.5" />
      </button>
      <Tooltip>
        <TooltipContent>{children}</TooltipContent>
      </Tooltip>
    </TooltipTrigger>
  );
}

