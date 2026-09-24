import { useState } from 'react';
import { Check, ContentCopy } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';

interface CopyButtonProps {
  /** The string copied to the clipboard. */
  value: string;
  /** Accessible label for screen-readers + tooltip. Defaults to "Copy". */
  copyLabel?: string;
  /**
   * When true, the button hides until the parent is hovered or
   * keyboard-focused. Defaults to `false` — detail rows have at most a
   * handful of these visible at a time, and a hover-only affordance
   * forces the operator to mouse over each row to discover Copy. Set to
   * true when used inside dense rows (e.g. table cells).
   */
  revealOnHover?: boolean;
  /** Optional className for the icon button. */
  className?: string;
}

/**
 * Standalone copy-to-clipboard icon button. Use when you want a small
 * Copy affordance next to a value that already renders its own typography
 * (e.g. inside a `<DetailRow>` next to `vm.name`) and wrapping the value
 * in `<CopyableText>` would change the layout.
 *
 * Visual: same content-copy / check icon swap as the inline
 * `<CopyableText>` button — kept in sync deliberately so the two read
 * as the same affordance at different sizes. Defaults to always-visible
 * (no hover-reveal) because detail-row context has at most one per row;
 * pass `revealOnHover` for table-cell context.
 */
export function CopyButton({
  value,
  copyLabel,
  revealOnHover = false,
  className,
}: CopyButtonProps) {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      toast(`Copied: ${value}`);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      toast('Failed to copy');
    }
  };

  return (
    <button
      type="button"
      onClick={copy}
      aria-label={copyLabel ?? `Copy ${value}`}
      title={copyLabel ?? 'Copy'}
      className={cn(
        'inline-flex size-5 shrink-0 cursor-pointer items-center justify-center rounded-sm text-muted-foreground opacity-60 transition-all hover:bg-muted hover:text-foreground hover:opacity-100 focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring',
        revealOnHover &&
          'opacity-0 group-hover:opacity-100 group-focus-within:opacity-100',
        className,
      )}
    >
      {copied ? <Check className="size-3 text-ok" /> : <ContentCopy className="size-3" />}
    </button>
  );
}