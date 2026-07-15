import { useState } from 'react';
import type { ReactNode } from 'react';
import { Check, ContentCopy } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';

interface CopyableTextProps {
  /** The string copied to the clipboard. */
  value: string;
  /** Visual label. Defaults to `value`. Pass a node for monospace IDs etc. */
  children?: ReactNode;
  /** Optional tooltip-style aria-label override for the copy button. */
  copyLabel?: string;
  className?: string;
}

/**
 * Inline copy-to-clipboard. Renders a value with a small ContentCopy button
 * that puts the raw string on the clipboard and toasts confirmation.
 *
 * Used inside table cells (ID, IP, hostname) and anywhere a value is
 * frequently re-typed or pasted into a terminal. The icon is muted at
 * rest and reveals its background on hover — stays out of the way
 * without hiding a useful affordance.
 */
export function CopyableText({ value, children, copyLabel, className }: CopyableTextProps) {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      toast(`Скопировано: ${value}`);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      toast('Не удалось скопировать');
    }
  };

  return (
    <span
      className={cn(
        'group/copy inline-flex items-center gap-1.5 rounded-sm transition-colors',
        className,
      )}
    >
      <span className="truncate">{children ?? value}</span>
      <button
        type="button"
        onClick={copy}
        aria-label={copyLabel ?? `Скопировать ${value}`}
        className="inline-flex size-5 shrink-0 items-center justify-center rounded-sm text-muted-foreground opacity-0 transition-all hover:bg-muted hover:text-foreground group-hover/copy:opacity-100 focus-visible:opacity-100"
      >
        {copied ? (
          <Check className="size-3 text-ok" />
        ) : (
          <ContentCopy className="size-3" />
        )}
      </button>
    </span>
  );
}