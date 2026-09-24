/**
 * Роут-дискавери — список URL-путей приложения, выведенный из файловых
 * роутов `src/routes/**` (TanStack Router file-route конвенция), плюс
 * карта SAMPLES с конкретными id для `$param`-роутов в мок-режиме.
 *
 * Используется `bun run shot routes`, `bun run shot page` (проверка
 * `$`-путей) и `agent:check` (маппинг изменённых файлов роутов на URL).
 */

import { readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { ROOT } from './servers';

export const ROUTES_DIR = join(ROOT, 'src', 'routes');

export interface RouteEntry {
  /** URL-путь, например '/vms/$id'. */
  readonly routePath: string;
  /** Файл-источник относительно корня console, например 'src/routes/vms/$id.tsx'. */
  readonly file: string;
  /** true, если путь содержит `$param`-сегмент. */
  readonly hasParam: boolean;
  /** Известный конкретный URL для `$param`-роута (см. SAMPLES), иначе undefined. */
  readonly sample?: string;
}

/**
 * Известные id для `$param`-роутов — фикстуры мок-флота/кластеров
 * стабильны (см. `src/mocks/vms.ts`, `src/shared/api/mocks/handmade/clusters.ts`),
 * поэтому эти id всегда резолвятся в мок-режиме.
 */
export const SAMPLES: Readonly<Record<string, string>> = {
  '/vms/$id': '/vms/vm-a8c91f2e',
  '/clusters/$id': '/clusters/cluster-prod-eu-1',
};

function walk(dir: string, acc: string[]): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    const st = statSync(full);
    if (st.isDirectory()) {
      walk(full, acc);
    } else if (/\.tsx?$/.test(name)) {
      acc.push(full);
    }
  }
  return acc;
}

/** Имя файла для скриншотов/отчётов: '/vms/new' → 'vms_new'; '/' → 'root'. */
export function slugForPath(routePath: string): string {
  if (routePath === '/') return 'root';
  return routePath.replace(/^\//, '').replace(/\//g, '_');
}

/** '/vms/route.tsx' → '/vms' (layout), '/vms/index.tsx' → '/vms' (leaf), '/vms/$id.tsx' → '/vms/$id'. */
function deriveRoutePath(relFile: string): { routePath: string; isLayout: boolean } {
  const noExt = relFile.replace(/\.tsx?$/, '');
  const segments = noExt.split('/');
  const last = segments[segments.length - 1];
  const isLayout = last === 'route';
  if (isLayout || last === 'index') {
    segments.pop();
  }
  const routePath = segments.length === 0 ? '/' : `/${segments.join('/')}`;
  return { routePath, isLayout };
}

/** Список роутов приложения, по одному на URL-путь (route.tsx layouts не
 *  перекрывают leaf-файл, если тот тоже маппится на тот же путь). */
export function listRoutes(): RouteEntry[] {
  const files = walk(ROUTES_DIR, []).sort();
  const byPath = new Map<string, RouteEntry>();

  for (const abs of files) {
    const rel = relative(ROUTES_DIR, abs).split(sep).join('/');
    if (/\.(test|stories)\.tsx?$/.test(rel)) continue;
    if (rel === '__root.tsx') continue;

    const { routePath, isLayout } = deriveRoutePath(rel);
    if (isLayout && byPath.has(routePath)) continue; // leaf wins over layout

    byPath.set(routePath, {
      routePath,
      file: `src/routes/${rel}`,
      hasParam: routePath.includes('$'),
      sample: SAMPLES[routePath],
    });
  }

  return Array.from(byPath.values()).sort((a, b) => a.routePath.localeCompare(b.routePath));
}

/**
 * URL-путь для отдельного файла роута, данного как repo-relative путь
 * (`src/routes/vms/$id.tsx` → `/vms/$id`). Возвращает null для файлов вне
 * `src/routes/`, тестов/стори и `__root.tsx` — используется `agent:check`
 * (шаг 5) при маппинге изменённых файлов на URL, без полного скана дерева.
 */
export function routePathForRouteFile(repoRelativeFile: string): string | null {
  const prefix = 'src/routes/';
  if (!repoRelativeFile.startsWith(prefix)) return null;
  const rel = repoRelativeFile.slice(prefix.length);
  if (/\.(test|stories)\.tsx?$/.test(rel)) return null;
  if (rel === '__root.tsx') return null;
  return deriveRoutePath(rel).routePath;
}
