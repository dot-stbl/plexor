import { useId, useRef, useState, type ReactNode } from 'react';
import { Help } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { cn } from '@/lib/utils';

/**
 * HelpTooltip — `?`-кружок рядом с label, раскрывает подсказку по hover/focus.
 * Приём из YC (help на каждом сложном поле). Триггер — button (фокусируемый,
 * a11y), не внутри `<label>` — чтобы не активировать контрол.
 *
 * Self-rolled instead of RAC's <Tooltip> + <TooltipTrigger>: in RAC 1.21.1 the
 * FocusableProvider inside TooltipTrigger silently no-ops when the trigger
 * has sibling children (the tooltip is itself a child). Verified in
 * Chromium: button never gets data-rac / aria-describedby, popup never
 * renders. ~30 lines of plain React for a tooltip that just works.
 */
export function HelpTooltip({
  children,
  className,
  delay = 200,
}: {
  children: ReactNode;
  className?: string;
  /** ms before the popup opens. Closes immediately on leave/blur. */
  delay?: number;
}) {
  const tooltipId = useId();
  const [open, setOpen] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  function show() {
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => setOpen(true), delay);
  }
  function hide() {
    if (timer.current) clearTimeout(timer.current);
    setOpen(false);
  }

  return (
    <span
      className="relative inline-flex"
      onMouseEnter={show}
      onMouseLeave={hide}
      onFocus={show}
      onBlur={hide}
    >
      <button
        type="button"
        aria-label="Help"
        aria-describedby={open ? tooltipId : undefined}
        className={cn(
          'inline-flex items-center text-muted-foreground/70 outline-none transition-colors hover:text-muted-foreground focus-visible:text-foreground',
          className,
        )}
      >
        <Help className="size-3.5" />
      </button>
      {open ? (
        <span
          id={tooltipId}
          role="tooltip"
          className={cn(
            'pointer-events-none absolute left-1/2 top-full z-50 mt-1.5 -translate-x-1/2 whitespace-nowrap rounded-md bg-foreground px-2 py-1 text-[0.6875rem] font-medium text-background shadow-md',
            'data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95',
            'data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95',
          )}
        >
          {children}
        </span>
      ) : null}
    </span>
  );
}
