/**
 * Lifecycle for the console's own mock dev server — started and killed
 * by THIS script, by PID, never by process name (see
 * `.agents/rules/process/agent-runtime-safety.md`). Mirrors the pattern
 * in `web/apps/console/scripts/agent/lib/servers.ts` (read-only
 * reference — console is off-limits to edit from here).
 */

import { spawn, type Subprocess } from 'bun';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { CONSOLE_ROOT, CONSOLE_VITE_BIN, TMP_DIR } from './paths';

const PID_FILE = join(TMP_DIR, 'console-mock-server.pid');
const LOG_FILE = join(TMP_DIR, 'console-mock-server.log');
const ERR_FILE = join(TMP_DIR, 'console-mock-server.err.log');

function killPid(pid: number): void {
  try {
    if (process.platform === 'win32') {
      // /T — this PID's own process tree (esbuild child etc.), never a
      // blanket kill by executable name.
      Bun.spawnSync(['taskkill', '/PID', String(pid), '/T', '/F'], { stdout: 'ignore', stderr: 'ignore' });
    } else {
      process.kill(pid, 'SIGTERM');
    }
  } catch {
    // already dead
  }
}

function killStalePid(): void {
  if (!existsSync(PID_FILE)) return;
  const pid = Number.parseInt(readFileSync(PID_FILE, 'utf8'), 10);
  if (Number.isFinite(pid)) killPid(pid);
  rmSync(PID_FILE, { force: true });
}

async function isUp(url: string): Promise<boolean> {
  try {
    const res = await fetch(url);
    return res.ok;
  } catch {
    return false;
  }
}

export interface RunningServer {
  readonly url: string;
  stop(): void;
}

export async function startConsoleMockServer(port: number, timeoutMs = 120_000): Promise<RunningServer> {
  mkdirSync(TMP_DIR, { recursive: true });
  killStalePid();

  const url = `http://127.0.0.1:${port}`;
  if (await isUp(`${url}/`)) {
    throw new Error(`port ${port} is already busy (not ours). Free it or edit lib/ports.ts and rerun.`);
  }

  const proc: Subprocess = spawn({
    cmd: ['node', CONSOLE_VITE_BIN, '--port', String(port), '--strictPort', '--host', '127.0.0.1'],
    cwd: CONSOLE_ROOT,
    env: { ...process.env, VITE_USE_MOCKS: 'true', BROWSER: 'none', FORCE_COLOR: '0' },
    stdout: Bun.file(LOG_FILE),
    stderr: Bun.file(ERR_FILE),
  });
  writeFileSync(PID_FILE, String(proc.pid));

  const stop = () => {
    killPid(proc.pid);
    rmSync(PID_FILE, { force: true });
  };

  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (proc.exitCode !== null) {
      stop();
      throw new Error(`console mock server exited with code ${proc.exitCode}. Log: ${LOG_FILE}`);
    }
    if (await isUp(`${url}/`)) return { url, stop };
    await Bun.sleep(400);
  }
  stop();
  throw new Error(`console mock server did not start in ${timeoutMs / 1000}s. Log: ${LOG_FILE}`);
}
