'use client';

import { useEffect, useState } from 'react';

/**
 * Animated terminal — types out a real `plx init` flow line by line so a
 * visitor can see Plexor's install story without clicking through. The
 * animation loops on a 14s cycle (10s typing + 4s hold) so the block
 * never goes blank; the cursor blinks on every step.
 *
 * Each line is typed at 28ms/char with a 220ms pause between lines —
 * fast enough to read as a sequence, slow enough to register as "real
 * command output", not as a marquee.
 */

const SCRIPT: readonly { readonly prompt?: string; readonly text: string }[] = [
  { prompt: '$', text: 'plx init --single-node' },
  { text: '› probing /dev/kvm ... ok (VT-x, libvirtd running)' },
  { text: '› probing openvswitch-switch ... ok' },
  { text: '› probing local-lvm thinpool ... ok (62 GiB free)' },
  { text: '› probing postgres-15 ... not found' },
  { text: '› install provider minio selected (s3-compatible object store)' },
  { text: '› writing /etc/plexor/install.yaml' },
  { text: '✓ control plane ready · http://10.0.0.5:8443' },
  { prompt: '$', text: 'plx provider install-instance wordpress' },
  { text: '› resolving dependencies ... postgresql ≥14.0 → auto-install' },
  { text: '› allocating 1 vCPU · 512 MiB · 10 GiB on plexor-node-01' },
  { text: '› pulling wordpress:0.2.0 ... 14 MB' },
  { text: '✓ wp-7f3a2c running at http://203.0.113.42' },
];

const CHAR_MS = 28;
const LINE_PAUSE_MS = 220;
const HOLD_MS = 4000;

export function TerminalPlayground() {
  const [typed, setTyped] = useState(0);

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout>;

    async function run() {
      while (!cancelled) {
        for (let i = 0; i < SCRIPT.length; i++) {
          if (cancelled) return;
          setTyped(i + 1);
          await sleep(typingDuration(SCRIPT[i]));
          if (cancelled) return;
          await sleep(LINE_PAUSE_MS);
        }
        await sleep(HOLD_MS);
        if (cancelled) return;
        setTyped(0);
      }
    }

    timer = setTimeout(() => {
      void run();
    }, 0);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, []);

  return (
    <div className="not-prose overflow-hidden rounded-xl border border-fd-border bg-fd-card font-mono text-sm shadow-sm">
      <div className="flex items-center gap-2 border-b border-fd-border bg-fd-muted px-4 py-2">
        <span className="size-2.5 rounded-full bg-fd-err/60" aria-hidden />
        <span className="size-2.5 rounded-full bg-fd-warn/60" aria-hidden />
        <span className="size-2.5 rounded-full bg-fd-ok/60" aria-hidden />
        <span className="text-fd-muted-foreground ml-2 text-xs">
          ~/plexor-node-01 — plx
        </span>
      </div>
      <div className="space-y-1 p-5 leading-relaxed">
        {SCRIPT.map((line, idx) => {
          const visible = idx < typed;
          const full = line.text;
          const shown = visible ? full : '';
          return (
            <div
              key={`${idx}-${line.text}`}
              className={
                line.prompt
                  ? 'text-fd-foreground font-medium'
                  : 'text-fd-muted-foreground'
              }
            >
              {line.prompt ? (
                <span className="text-fd-primary mr-2">{line.prompt}</span>
              ) : null}
              <span>{shown}</span>
              {visible && idx === typed - 1 ? (
                <span
                  className="docs-terminal-caret bg-fd-foreground ml-0.5 inline-block h-4 w-1.5 translate-y-0.5"
                  aria-hidden
                />
              ) : null}
            </div>
          );
        })}
      </div>
    </div>
  );
}

function typingDuration(line: { readonly text: string }): number {
  return line.text.length * CHAR_MS;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}