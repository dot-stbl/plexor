import { useEffect, useId, useRef, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
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
 *
 * Clipping fix: the popup is portalled into document.body and positioned via
 * fixed coordinates derived from the trigger's getBoundingClientRect().
 * This sidesteps two stacked traps in dense layouts — (a) ancestor cards
 * with `overflow: hidden` clip absolute-positioned descendants, and (b)
 * ancestor transforms/filters/isolation create a new containing block that
 * pins the popup inside the wrong stacking context. Coordinates refresh on
 * scroll, resize, and ResizeObserver (e.g. sidebar collapse).
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
  const [pos, setPos] = useState<{ left: number; top: number } | null>(null);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  function show() {
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => {
      if (triggerRef.current) {
        const rect = triggerRef.current.getBoundingClientRect();
        setPos({ left: rect.left + rect.width / 2, top: rect.bottom });
      }
      setOpen(true);
    }, delay);
  }
  function hide() {
    if (timer.current) clearTimeout(timer.current);
    setOpen(false);
  }

  // While open, keep the popup pinned to the trigger. Triggers: window scroll,
  // window resize, layout changes (ResizeObserver on the trigger).
  useEffect(() => {
    if (!open) return;
    const trigger = triggerRef.current;
    if (!trigger) return;

    const update = () => {
      const rect = trigger.getBoundingClientRect();
      setPos({ left: rect.left + rect.width / 2, top: rect.bottom });
    };

    update();
    window.addEventListener('scroll', update, true);
    window.addEventListener('resize', update);
    const ro = new ResizeObserver(update);
    ro.observe(trigger);

    return () => {
      window.removeEventListener('scroll', update, true);
      window.removeEventListener('resize', update);
      ro.disconnect();
    };
  }, [open]);

  return (
    <span
      className="relative inline-flex"
      onMouseEnter={show}
      onMouseLeave={hide}
      onFocus={show}
      onBlur={hide}
    >
      <button
        ref={triggerRef}
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
      {open && pos
        ? createPortal(
            <span
              id={tooltipId}
              role="tooltip"
              style={{ left: pos.left, top: pos.top }}
              className={cn(
                'pointer-events-none fixed z-tooltip mt-1.5 -translate-x-1/2 whitespace-nowrap rounded-md bg-foreground px-2 py-1 text-[0.6875rem] font-medium text-background shadow-md',
                'animate-in fade-in-0 zoom-in-95 duration-100',
              )}
            >
              {children}
            </span>,
            document.body,
          )
        : null}
    </span>
  );
}
