import type { ReactNode } from 'react';
import { HelpTooltip } from '@/shared/ui/primitives/help-tooltip';
import { cn } from '@/lib/utils';

/**
 * FieldRow — горизонтальное поле config-формы (эталон YC): label-колонка слева
 * (+ `?` help, + красная `*` required), контрол справа. На узком — стек.
 * Крошки/чром — не тут; это только строка формы. См. patterns.md §5-6.
 */
export interface FieldRowProps {
  label: ReactNode;
  /** id контрола для label→control association. */
  htmlFor?: string;
  required?: boolean;
  /** Текст `?`-подсказки. */
  help?: ReactNode;
  /** Пояснение под контролом. */
  description?: ReactNode;
  children: ReactNode;
  className?: string;
}

export function FieldRow({ label, htmlFor, required, help, description, children, className }: FieldRowProps) {
  return (
    <div
      role="group"
      className={cn(
        'grid grid-cols-1 gap-1.5 py-2 sm:grid-cols-[minmax(140px,200px)_1fr] sm:items-start sm:gap-4',
        className,
      )}
    >
      <div className="flex items-center gap-1.5 sm:pt-1.5">
        <label htmlFor={htmlFor} className="text-sm font-medium text-foreground">
          {label}
        </label>
        {required && (
          <span className="text-destructive" aria-hidden>
            *
          </span>
        )}
        {help && <HelpTooltip>{help}</HelpTooltip>}
      </div>
      <div className="min-w-0 space-y-1.5">
        {children}
        {description && <p className="text-xs text-muted-foreground">{description}</p>}
      </div>
    </div>
  );
}
