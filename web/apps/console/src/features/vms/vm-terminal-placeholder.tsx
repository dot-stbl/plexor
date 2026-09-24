import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Terminal } from '@xterm/xterm';
import { FitAddon } from '@xterm/addon-fit';

import '@xterm/xterm/css/xterm.css';

interface TerminalPlaceholderProps {
  /** VM id — interpolated into the "would-be" WebSocket URL banner. */
  vmId: string;
}

/**
 * Terminal placeholder for the VM Console & terminal card.
 *
 * Renders a real `@xterm/xterm` terminal surface so the operator sees a
 * familiar terminal UI — but it is not connected to any backend. We
 * mount the terminal, fit it to the container, print a banner that
 * explains the missing Plexor.NodeAgent and the URL a real session
 * would target, then accept keystrokes locally so the user can poke at
 * the cursor (the typing doesn't go anywhere, which is the honest
 * behaviour — no fake "command not found" loops that would pretend the
 * stream is live).
 *
 * The component cleans up the terminal instance + fit addon on unmount.
 *
 * Why `xterm.js` and not a styled `<pre>`: the brief calls for a real
 * terminal UI — the operator should see what the eventual integration
 * will look like. xterm.js handles focus, blinking cursor, key
 * encoding (no terminal protocol yet — those bytes stay in xterm.js's
 * buffer), selection and copy.
 */
export function TerminalPlaceholder({ vmId }: TerminalPlaceholderProps) {
  const { t } = useTranslation();
  const containerRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    // xterm's theme parser only accepts literal color strings (no CSS
    // vars), so read the --terminal-* tokens from the computed style —
    // the CSS surface and the canvas stay in sync from one source.
    const styles = getComputedStyle(container);
    const background = styles.getPropertyValue('--terminal-bg').trim();
    const foreground = styles.getPropertyValue('--terminal-fg').trim();

    const terminal = new Terminal({
      convertEol: true,
      cursorBlink: true,
      fontFamily:
        'ui-monospace, SFMono-Regular, "JetBrains Mono", Menlo, Consolas, monospace',
      fontSize: 12,
      theme: {
        background,
        foreground,
        cursor: foreground,
      },
      disableStdin: false,
      cursorStyle: 'block',
    });

    const fit = new FitAddon();
    terminal.loadAddon(fit);
    terminal.open(container);

    let cancelled = false;
    requestAnimationFrame(() => {
      if (cancelled || !container.isConnected) return;
      try {
        fit.fit();
      } catch {
        /* container has zero size at first paint — ignore, next resize picks up */
      }
    });

    const resize = () => {
      try {
        fit.fit();
      } catch {
        /* container unmounted mid-resize */
      }
    };
    window.addEventListener('resize', resize);

    // Banner explains the backend integration that's missing.
    terminal.writeln(
      `\x1b[33m${t('vms.detail.console.terminalUnavailable')}\x1b[0m`,
    );
    terminal.writeln(
      `\x1b[90m${t('vms.detail.console.terminalUnavailableDetail', { id: vmId })}\x1b[0m`,
    );
    terminal.writeln('');
    terminal.write('\x1b[32m$\x1b[0m ');

    // Local echo — typing stays in xterm.js's buffer; nothing is sent
    // anywhere. This is the honest placeholder behaviour: the user
    // sees what the eventual integration looks like, but no fake
    // "command not found" loops that would pretend to be live.
    const disposable = terminal.onData((data: string) => {
      if (data === '\r') {
        terminal.write('\r\n');
        terminal.write('\x1b[32m$\x1b[0m ');
        return;
      }
      if (data === '\x7f') {
        terminal.write('\b \b');
        return;
      }
      terminal.write(data);
    });

    return () => {
      cancelled = true;
      window.removeEventListener('resize', resize);
      disposable.dispose();
      terminal.dispose();
    };
  }, [t, vmId]);

  return (
    <div
      ref={containerRef}
      data-od-id="vm-terminal-placeholder"
      className="h-64 w-full overflow-hidden rounded-md border border-border bg-(--terminal-bg) p-2"
    />
  );
}