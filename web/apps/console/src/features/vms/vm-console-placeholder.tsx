import { useTranslation } from 'react-i18next';
import { VideocamOff } from '@nine-thirty-five/material-symbols-react/rounded/700';

interface ConsolePlaceholderProps {
  /** VM id — used for the data-od-id so devtools / visual tests can target. */
  vmId: string;
}

/**
 * noVNC placeholder for the VM Console & terminal card.
 *
 * Per the brief, the actual streaming implementation is out of scope (it
 * requires a noVNC server endpoint on the Plexor.NodeAgent). We render
 * an inert container that looks like a console window — dark surface,
 * monospace text — so the operator sees where the stream will land.
 *
 * We deliberately avoid pulling in `@novnc/novnc` (2 MB minified) and
 * starting it without a backend: it would render an empty canvas and
 * confuse the "is this thing on?" test. An honest "unavailable" panel
 * is better than a fake stream.
 */
export function ConsolePlaceholder({ vmId }: ConsolePlaceholderProps) {
  const { t } = useTranslation();

  return (
    <div
      data-od-id={`vm-console-placeholder-${vmId}`}
      className="flex h-64 w-full flex-col items-center justify-center gap-3 rounded-md border border-border bg-[#0b0d10] p-6 text-center"
    >
      <VideocamOff className="size-8 text-muted-foreground" aria-hidden />
      <p className="font-mono text-xs text-foreground/80">
        {t('vms.detail.console.consoleUnavailable')}
      </p>
      <p className="max-w-sm font-mono text-[11px] text-muted-foreground">
        {t('vms.detail.console.consoleUnavailableDetail')}
      </p>
    </div>
  );
}