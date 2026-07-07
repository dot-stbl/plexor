import { cn } from '@/lib/utils';

import type { ComponentProps } from 'react';

/**
 * Console — terminal / serial-log panel.
 *
 * Abstract: any "fixed-width mono terminal output" surface (boot logs,
 * audit trails, raw API responses, debug panels).
 */
export interface ConsoleProps extends ComponentProps<'pre'> {
  /** Optional prompt character (e.g. "$", "#", ">"). */
  prompt?: string;
}

export function Console({ prompt, className, children, ...props }: ConsoleProps) {
  return (
    <pre
      data-slot="console"
      className={cn(
        'rounded-md p-3 font-mono text-xs leading-relaxed whitespace-pre-wrap',
        'bg-[oklch(18%_0.02_240)] text-[oklch(92%_0.008_240)]',
        className,
      )}
      {...props}
    >
      {prompt && (
        <span aria-hidden className="select-none">
          {prompt}{' '}
        </span>
      )}
      {children}
    </pre>
  );
}

export type ConsoleLineVariant = 'ok' | 'err' | 'muted';

const LINE_COLOR: Record<ConsoleLineVariant, string> = {
  ok: 'text-[oklch(75%_0.1_145)]',
  err: 'text-[oklch(75%_0.16_25)]',
  muted: 'text-[oklch(70%_0.008_240)]',
};

export interface ConsoleLineProps extends ComponentProps<'span'> {
  variant?: ConsoleLineVariant;
}

export function ConsoleLine({ variant = 'muted', className, children, ...props }: ConsoleLineProps) {
  return (
    <span data-slot="console-line" data-variant={variant} className={cn(LINE_COLOR[variant], className)} {...props}>
      {children}
    </span>
  );
}
