import type { ReactNode } from 'react';
import { Question } from '@/shared/ui/icon';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/shared/ui/primitives/tooltip';
import { cn } from '@/lib/utils';

/**
 * HelpTooltip — `?`-кружок рядом с label, раскрывает подсказку по hover/focus.
 * Приём из YC (help на каждом сложном поле). Триггер — button (фокусируемый,
 * a11y), не внутри `<label>` — чтобы не активировать контрол.
 */
export function HelpTooltip({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <TooltipProvider delay={200}>
      <Tooltip>
        <TooltipTrigger
          render={
            <button
              type="button"
              aria-label="Help"
              className={cn(
                'inline-flex items-center text-muted-foreground/70 outline-none transition-colors hover:text-muted-foreground focus-visible:text-foreground',
                className,
              )}
            />
          }
        >
          <Question className="size-3.5" />
        </TooltipTrigger>
        <TooltipContent>{children}</TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
