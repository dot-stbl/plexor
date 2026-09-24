/**
 * Path constants for the capture pipeline. Everything is derived from
 * `import.meta.dir` so the script works regardless of the caller's cwd
 * (bun run scripts/capture/capture.ts from web/apps/www, or `bun run
 * capture` via package.json).
 */

import { join } from 'node:path';
import { tmpdir } from 'node:os';

export const CAPTURE_DIR = join(import.meta.dir, '..');
export const WWW_ROOT = join(CAPTURE_DIR, '..', '..');
export const WEB_ROOT = join(WWW_ROOT, '..', '..');
export const CONSOLE_ROOT = join(WEB_ROOT, 'apps', 'console');

export const CONSOLE_VITE_BIN = join(CONSOLE_ROOT, 'node_modules', 'vite', 'bin', 'vite.js');

export const MEDIA_OUT_DIR = join(WWW_ROOT, 'public', 'media');

/**
 * Scratch space for raw PNG screenshots + server logs/pid — lives in
 * the OS temp dir, never inside the repo, so a crashed run never leaves
 * stray files for `git status` to report. Overridable via env for local
 * debugging (`CAPTURE_TMP_DIR=...`).
 */
export const TMP_DIR = process.env.CAPTURE_TMP_DIR ?? join(tmpdir(), 'plexor-capture-tmp');
