import { useState } from 'react';
import { Check, ContentCopy } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { cn } from '@/lib/utils';

interface CopyButtonProps {
  /** The string copied to the clipboard. */
  value: string;
  /** Accessible label for screen-readers + tooltip. Defaults to "Copy". */
  copyLabel?: string;
  /**
   * When true, the button hides until the parent is hovered or
   * keyboard-focused. Defaults to `false` — most www call sites show at
   * most one of these at a time, and a hover-only affordance would force
   * the visitor to mouse over the block to discover Copy.
   */
  revealOnHover?: boolean;
  /** Optional className for the icon button. */
  className?: string;
}

/**
 * Standalone copy-to-clipboard icon button — adapted from console's
 * `src/shared/ui/primitives/copy-button.tsx` for `www`: the original uses
 * `sonner` toasts and Russian copy ("Скопировано: …"), neither of which
 * apply here (www has no toast dependency in scope, per the redesign
 * spec, and UI copy is English-only). Same visual affordance instead: a
 * 1.5s local `copied` state swaps the icon to a checkmark, mirroring the
 * spec's "no toast library dependency needed" guidance (§3.2).
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
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      // Clipboard unavailable (permissions/insecure context) — no visible
      // feedback beyond the icon simply not flipping to the checkmark.
    }
  };

  return (
    <button
      type="button"
      onClick={() => void copy()}
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
