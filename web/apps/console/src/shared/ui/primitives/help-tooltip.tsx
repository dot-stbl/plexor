import type { ReactNode } from 'react';
import { Tooltip, TooltipTrigger } from 'react-aria-components';
import { Help } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { cn } from '@/lib/utils';

/**
 * HelpTooltip — `?`-кружок рядом с label, раскрывает подсказку по hover/focus.
 * Приём из YC (help на каждом сложном поле). Триггер — button (фокусируемый,
 * a11y), не внутри `<label>` — чтобы не активировать контрол.
 *
 * Implementation note: previous versions routed through the Plexor Tooltip
 * compat shim OR wrapped a second RAC Tooltip around our TooltipContent shim
 * — both produced a popup that never actually rendered (hover/focus timers
 * fired but the popup stayed hidden, in one case because of a dead context,
 * in the other because of nested RAC Tooltips shadowing each other).
 * This version uses react-aria-components primitives directly with the popup
 * styling inlined on the RAC Tooltip via className — no shim, no nesting.
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
      <Tooltip
        className={cn(
          'z-50 inline-flex w-fit max-w-xs origin-(--transform-origin) items-center gap-1.5 rounded-md bg-foreground px-3 py-1.5 text-xs text-background',
          'data-[placement=bottom]:slide-in-from-top-2 data-[placement=left]:slide-in-from-right-2 data-[placement=right]:slide-in-from-left-2 data-[placement=top]:slide-in-from-bottom-2 data-[placement=inline-end]:slide-in-from-left-2 data-[placement=inline-start]:slide-in-from-right-2',
          'data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95 data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95',
        )}
        offset={6}
        placement="top"
      >
        {children}
      </Tooltip>
    </TooltipTrigger>
  );
}


