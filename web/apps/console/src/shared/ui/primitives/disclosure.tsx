import { useState, type ReactNode } from 'react';
import { KeyboardArrowRight } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/shared/ui/primitives/collapsible';
import { cn } from '@/lib/utils';

/**
 * Disclosure — раскрывающийся блок «Advanced» поверх shadcn `Collapsible`
 * (Base UI). НЕ сырой div/button и НЕ `Accordion` (тот держит фикс-высоту).
 *
 * `variant`:
 *  - `inline` (default): текст-триггер с каретом слева, контент под ним —
 *    для «продвинутых» полей прямо в теле карточки-секции.
 *  - `card`: разворачиваемая карточка — бордер + кликабельная шапка (лейбл
 *    слева, карет справа), контент внутри рамки.
 */
export interface DisclosureProps {
  /** Текст-триггер (напр. «Advanced · CPU type, NUMA»). */
  summary: ReactNode;
  children: ReactNode;
  defaultOpen?: boolean;
  variant?: 'inline' | 'card';
  className?: string;
}

export function Disclosure({
  summary,
  children,
  defaultOpen = false,
  variant = 'inline',
  className,
}: DisclosureProps) {
  const [open, setOpen] = useState(defaultOpen);
  const caret = (
    <KeyboardArrowRight className={cn('size-3.5 shrink-0 transition-transform', open && 'rotate-90')} />
  );

  if (variant === 'card') {
    return (
      <Collapsible
        open={open}
        onOpenChange={setOpen}
        className={cn('overflow-hidden rounded-md border border-border', className)}
      >
        <CollapsibleTrigger
          className={cn(
            'flex w-full items-center justify-between gap-2 px-3 py-2 text-xs font-medium text-muted-foreground outline-none transition-colors hover:bg-muted/50 hover:text-foreground focus-visible:text-foreground',
            open && 'text-foreground',
          )}
        >
          <span>{summary}</span>
          {caret}
        </CollapsibleTrigger>
        <CollapsibleContent className="flex flex-col gap-2 border-t border-border px-3 py-3">
          {children}
        </CollapsibleContent>
      </Collapsible>
    );
  }

  return (
    <Collapsible open={open} onOpenChange={setOpen} className={className}>
      <CollapsibleTrigger className="flex items-center gap-1 text-xs font-medium text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:text-foreground">
        {caret}
        {summary}
      </CollapsibleTrigger>
      <CollapsibleContent className="mt-2 flex flex-col gap-2 border-t border-border pt-2">
        {children}
      </CollapsibleContent>
    </Collapsible>
  );
}
