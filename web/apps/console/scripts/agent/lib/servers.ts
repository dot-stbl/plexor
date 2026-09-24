/**
 * Жизненный цикл dev-серверов для агентских скриншотов.
 *
 * Один запуск = поднять сервер → отработать → убить по PID. PID пишется в
 * `.shots/.<name>.pid`; если прошлый прогон упал и оставил сервер, следующий
 * прогон убивает именно этот PID (никаких `taskkill /IM node.exe` — см.
 * .agents/rules/process/agent-runtime-safety.md).
 */

import { spawn, type Subprocess } from 'bun';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

export const ROOT = join(import.meta.dir, '..', '..', '..');
export const SHOTS_DIR = join(ROOT, '.shots');

export interface ServerSpec {
  readonly name: 'app' | 'storybook';
  readonly port: number;
  readonly readyPath: string;
  readonly cmd: string[];
  readonly env?: Record<string, string>;
}

export const APP_SERVER: ServerSpec = {
  name: 'app',
  port: 17150,
  readyPath: '/',
  cmd: ['node', join(ROOT, 'node_modules', 'vite', 'bin', 'vite.js'), '--port', '17150', '--strictPort', '--host', '127.0.0.1'],
  env: { VITE_USE_MOCKS: 'true' },
};

export const STORYBOOK_SERVER: ServerSpec = {
  name: 'storybook',
  port: 6107,
  readyPath: '/index.json',
  cmd: [
    'node',
    join(ROOT, 'node_modules', 'storybook', 'bin', 'index.cjs'),
    'dev',
    '--ci',
    '--no-open',
    '--quiet',
    '--port',
    '6107',
    '--host',
    '127.0.0.1',
  ],
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
      // /T — дерево процессов ЭТОГО PID (esbuild и т.п.), не blanket kill по имени.
      Bun.spawnSync(['taskkill', '/PID', String(pid), '/T', '/F'], { stdout: 'ignore', stderr: 'ignore' });
    } else {
      process.kill(pid, 'SIGTERM');
    }
  } catch {
    // процесс уже умер
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
