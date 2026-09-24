/**
 * Список изменённых файлов (tracked diff vs HEAD + untracked) относительно
 * корня console-приложения, с прямыми слэшами. Общий код для
 * `agent:rules` (дефолтный scope) и `agent:check` (шаг 5 — что шотить).
 */

import { execFileSync } from 'node:child_process';
import { ROOT } from './servers';

function gitList(args: string[]): string[] {
  try {
    return execFileSync('git', args, { cwd: ROOT, encoding: 'utf8' })
      .split('\n')
      .map((s) => s.trim())
      .filter(Boolean);
  } catch {
    return [];
  }
}

export function listChangedFiles(): string[] {
  // `--relative` — иначе git печатает пути от корня монорепо
  // (`web/apps/console/src/...`), а не от cwd.
  const tracked = gitList(['diff', '--name-only', 'HEAD', '--relative']);
  const untracked = gitList(['ls-files', '--others', '--exclude-standard']);
  return Array.from(new Set([...tracked, ...untracked])).map((f) => f.split('\\').join('/'));
}
