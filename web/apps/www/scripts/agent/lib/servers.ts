/**
 * Dev-server lifecycle for agent screenshots.
 *
 * One run = start the server → do the work → kill it by PID. The PID is
 * written to `.shots/.<name>.pid`; if a previous run crashed and left a
 * server behind, the next run kills that exact PID (never a blanket
 * `taskkill /IM node.exe` — see `.agents/rules/process/agent-runtime-safety.md`).
 *
 * Port: 17111 — `www`'s dev port (17101) is reserved for the operator's
 * own dev server (see `web/docs/PORTS.md`); this tool never touches it.
 * 17111 is `www`'s documented Vite *preview* port, but this spawns a
 * plain `vite` dev server there instead of a build+preview cycle (same
 * approach console's own `shot` tool uses on its own scratch port) —
 * faster iteration, and nothing else runs a preview server on 17111
 * during normal agent work.
 */

import { spawn, type Subprocess } from 'bun';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

export const ROOT = join(import.meta.dir, '..', '..', '..');
export const SHOTS_DIR = join(ROOT, '.shots');

export interface ServerSpec {
  readonly name: 'app';
  readonly port: number;
  readonly readyPath: string;
  readonly cmd: string[];
  readonly env?: Record<string, string>;
}

export const APP_SERVER: ServerSpec = {
  name: 'app',
  port: 17111,
  readyPath: '/',
  cmd: ['node', join(ROOT, 'node_modules', 'vite', 'bin', 'vite.js'), '--port', '17111', '--strictPort', '--host', '127.0.0.1'],
};

export function baseUrl(spec: ServerSpec): string {
  return `http://127.0.0.1:${spec.port}`;
}

function pidFile(spec: ServerSpec): string {
  return join(SHOTS_DIR, `.${spec.name}.pid`);
}

function killPid(pid: number): void {
  try {
    if (process.platform === 'win32') {
      // /T — the process TREE rooted at this PID (esbuild workers etc.),
      // not a blanket kill by name.
      Bun.spawnSync(['taskkill', '/PID', String(pid), '/T', '/F'], { stdout: 'ignore', stderr: 'ignore' });
    } else {
      process.kill(pid, 'SIGTERM');
    }
  } catch {
    // Already dead.
  }
}

function killStale(spec: ServerSpec): void {
  const file = pidFile(spec);
  if (!existsSync(file)) return;
  const pid = Number.parseInt(readFileSync(file, 'utf8'), 10);
  if (Number.isFinite(pid)) killPid(pid);
  rmSync(file, { force: true });
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

export async function startServer(spec: ServerSpec, timeoutMs = 120_000): Promise<RunningServer> {
  mkdirSync(SHOTS_DIR, { recursive: true });
  killStale(spec);

  const url = baseUrl(spec);
  if (await isUp(url + spec.readyPath)) {
    throw new Error(
      `port ${spec.port} is already busy (not ours). Stop whatever listens on ${url} and rerun.`,
    );
  }

  const logPath = join(SHOTS_DIR, `.${spec.name}.log`);
  const proc: Subprocess = spawn({
    cmd: spec.cmd,
    cwd: ROOT,
    env: { ...process.env, ...spec.env, BROWSER: 'none', FORCE_COLOR: '0' },
    stdout: Bun.file(logPath),
    stderr: Bun.file(logPath.replace(/\.log$/, '.err.log')),
  });
  writeFileSync(pidFile(spec), String(proc.pid));

  const stop = () => {
    killPid(proc.pid);
    rmSync(pidFile(spec), { force: true });
  };

  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (proc.exitCode !== null) {
      stop();
      throw new Error(`${spec.name} server exited with code ${proc.exitCode}. Log: ${logPath}`);
    }
    if (await isUp(url + spec.readyPath)) {
      return { url, stop };
    }
    await Bun.sleep(500);
  }
  stop();
  throw new Error(`${spec.name} server did not start in ${timeoutMs / 1000}s. Log: ${logPath}`);
}
