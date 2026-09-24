#!/usr/bin/env bun
/**
 * Visual regression test orchestrator.
 *
 * Owns the full lifecycle of running the Storybook test-runner against the
 * static Storybook build:
 *   1. Build the static Storybook (`bun run build:storybook`).
 *   2. Spawn `scripts/serve-static.ts` as a child process on port 6006.
 *   3. Wait until the server responds (HTTP HEAD on `/index.json`).
 *   4. Run `bunx test-storybook --url http://127.0.0.1:6006` with
 *      `--updateSnapshot` if `--update` was passed.
 *   5. Kill the server.
 *   6. Clean up `test-results/`, `playwright-report/`, and the diff-output
 *      directory jest-image-snapshot creates.
 *   7. Exit with the test-runner's exit code.
 *
 * Cross-platform: spawns child processes with detached + kill by PID. The
 * previous `bun run ... &` + `kill $PID` approach failed on Windows
 * because PowerShell has no `&` job control; doing the orchestration in
 * code means the same script works on both.
 *
 * Args:
 *   --update        Pass `--updateSnapshot` to test-storybook (regenerate baselines).
 *   --cleanup-only  Skip build + test, just remove test artifacts. Useful
 *                   for "I have uncommitted diff PNGs from a failed run".
 *   --skip-build    Don't rebuild Storybook (use existing dist-storybook/).
 *                   Useful when iterating on test-runner.ts.
 *   --no-cleanup    Skip step 6 (keep test-results/ for inspection).
 */

import { spawn, type Subprocess } from 'bun';
import { existsSync, rmSync } from 'node:fs';
import { join, resolve } from 'node:path';

// Port override: the default 6006 may be held by another process (dev
// storybook, a sibling agent's run). VISUAL_PORT lets a run pick a free
// port without editing the script.
const PORT = Number.parseInt(process.env.VISUAL_PORT ?? '6006', 10);
const URL = `http://127.0.0.1:${PORT}`;
const ROOT = resolve(import.meta.dir, '..');
const BUILD_DIR = join(ROOT, 'dist-storybook');
const SERVE_SCRIPT = join(ROOT, 'scripts', 'serve-static.ts');

const args = new Set(process.argv.slice(2));
const update = args.has('--update');
const cleanupOnly = args.has('--cleanup-only');
const skipBuild = args.has('--skip-build');
const noCleanup = args.has('--no-cleanup');

const ARTIFACTS_TO_CLEAN = [
  join(ROOT, 'test-results'),
  join(ROOT, 'playwright-report'),
  join(ROOT, '.storybook', '__screenshots__', '__diff_output__'),
];

function cleanup() {
  for (const p of ARTIFACTS_TO_CLEAN) {
    try {
      rmSync(p, { recursive: true, force: true });
      console.log(`[visual] removed ${p}`);
    } catch (err) {
      if ((err as NodeJS.ErrnoException).code !== 'ENOENT') {
        console.error(`[visual] failed to remove ${p}:`, (err as Error).message);
      }
    }
  }
}

if (cleanupOnly) {
  cleanup();
  process.exit(0);
}

async function waitForServer(url: string, timeoutMs = 60_000): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const res = await fetch(url, { method: 'HEAD' });
      if (res.ok || res.status === 200 || res.status === 304) {
        return;
      }
    } catch {
      // not yet ready
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error(`server at ${url} did not become ready within ${timeoutMs}ms`);
}

let server: Subprocess | null = null;

async function run(): Promise<number> {
  if (!skipBuild) {
    console.log('[visual] building static storybook…');
    const build = spawn({
      cmd: ['bun', 'run', 'build:storybook'],
      cwd: ROOT,
      stdout: 'inherit',
      stderr: 'inherit',
    });
    const buildCode = await build.exited;
    if (buildCode !== 0) {
      console.error(`[visual] build failed (exit ${buildCode})`);
      return buildCode;
    }
  } else if (!existsSync(BUILD_DIR)) {
    console.error(`[visual] --skip-build passed but ${BUILD_DIR} does not exist`);
    return 1;
  }

  console.log(`[visual] starting static server on ${URL}…`);
  server = spawn({
    cmd: ['bun', 'run', SERVE_SCRIPT, BUILD_DIR, String(PORT)],
    cwd: ROOT,
    stdout: 'inherit',
    stderr: 'inherit',
    env: { ...process.env, FORCE_COLOR: '1' },
  });

  try {
    await waitForServer(URL);
    console.log('[visual] server ready, running test-storybook…');

    // Use `bun x` (workspace-local binary resolution) instead of the
    // global `bunx` CLI which may not see workspace devDeps on Windows.
    const testArgs = ['bun', 'x', 'test-storybook', '--url', URL];
    if (update) testArgs.push('--updateSnapshot');

    const test = spawn({
      cmd: testArgs,
      cwd: ROOT,
      stdout: 'inherit',
      stderr: 'inherit',
      env: { ...process.env, FORCE_COLOR: '1' },
    });

    const testCode = await test.exited;
    return testCode;
  } finally {
    if (server) {
      server.kill();
      console.log('[visual] server stopped');
    }
    if (!noCleanup) {
      cleanup();
    }
  }
}

const code = await run();
process.exit(code);
