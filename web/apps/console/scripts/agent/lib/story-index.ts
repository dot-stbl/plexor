/**
 * Storybook `/index.json` — список известных story id (id, title,
 * importPath) для `shot stories`, валидации `shot story <id>` и подсказок
 * при опечатке (5 ближайших id по расстоянию Левенштейна).
 */

export interface StoryIndexEntry {
  readonly id: string;
  readonly title: string;
  readonly name: string;
  readonly importPath: string;
}

interface RawIndexEntry {
  readonly id?: unknown;
  readonly title?: unknown;
  readonly name?: unknown;
  readonly importPath?: unknown;
  readonly type?: unknown;
}

export async function fetchStoryIndex(storybookBaseUrl: string): Promise<StoryIndexEntry[]> {
  const res = await fetch(`${storybookBaseUrl}/index.json`);
  if (!res.ok) {
    throw new Error(`Failed to fetch Storybook index.json: HTTP ${res.status}`);
  }
  const json = (await res.json()) as { entries?: Record<string, RawIndexEntry> };
  const entries = json.entries ?? {};
  return Object.values(entries)
    .filter((e) => e.type !== 'docs') // docs-only entries aren't renderable stories
    .map((e) => ({
      id: String(e.id ?? ''),
      title: String(e.title ?? ''),
      name: String(e.name ?? ''),
      importPath: String(e.importPath ?? ''),
    }))
    .filter((e) => e.id.length > 0)
    .sort((a, b) => a.id.localeCompare(b.id));
}

/** Итеративное расстояние Левенштейна — без внешних зависимостей. */
function levenshtein(a: string, b: string): number {
  const prevRow = Array.from({ length: b.length + 1 }, (_, j) => j);
  for (let i = 1; i <= a.length; i++) {
    let diagonal = prevRow[0];
    prevRow[0] = i;
    for (let j = 1; j <= b.length; j++) {
      const temp = prevRow[j];
      prevRow[j] = a[i - 1] === b[j - 1] ? diagonal : 1 + Math.min(diagonal, prevRow[j], prevRow[j - 1]);
      diagonal = temp;
    }
  }
  return prevRow[b.length];
}

/** 5 ближайших известных id к опечатанному — по расстоянию Левенштейна. */
export function suggestStoryIds(unknownId: string, all: readonly StoryIndexEntry[], count = 5): string[] {
  return all
    .map((e) => ({ id: e.id, distance: levenshtein(unknownId, e.id) }))
    .sort((a, b) => a.distance - b.distance)
    .slice(0, count)
    .map((e) => e.id);
}

/** Подстрочный фильтр по id или title — для `shot stories [filter]`. */
export function filterStories(all: readonly StoryIndexEntry[], filter: string | undefined): StoryIndexEntry[] {
  if (!filter) return all.slice();
  const needle = filter.toLowerCase();
  return all.filter((e) => e.id.toLowerCase().includes(needle) || e.title.toLowerCase().includes(needle));
}
