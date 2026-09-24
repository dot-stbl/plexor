/**
 * Проверки отрендеренной страницы — «глаза» агента в текстовом виде.
 *
 * Модель может не уметь смотреть PNG, поэтому всё, что видно глазами,
 * дублируется текстом: ошибки консоли, упавшие запросы, горизонтальный
 * скролл, обрезанный текст, кнопки без имени, непереведённые ключи i18n,
 * плюс aria-снимок структуры страницы.
 */

import type { ConsoleMessage, Page, Request, Response } from 'playwright';

export type Severity = 'FAIL' | 'WARN';

export interface Issue {
  readonly severity: Severity;
  readonly code: string;
  readonly message: string;
  readonly hint: string;
}

/** Шум, который не является проблемой страницы. */
const CONSOLE_NOISE = [
  /Download the React DevTools/i,
  /\[vite\]/i,
  /\[MSW\] Mocking enabled/i,
  /i18next is maintained with support/i,
  /boot: failed to apply community theme/i,
  /Lit is in dev mode/i,
];

export interface PageRecorder {
  readonly issues: Issue[];
  detach(): void;
}

/**
 * Known console messages with an actionable hint, matched against the raw
 * message text (before it's truncated/annotated with a location). Checked
 * in order, first match wins. Add a row here whenever the same console
 * warning/error keeps showing up across pages with only the generic
 * "read the message" fallback — that fallback is for messages nobody has
 * triaged yet, not a permanent home for known, recurring noise.
 */
const KNOWN_CONSOLE_HINTS: ReadonlyArray<{ readonly test: RegExp; readonly hint: string }> = [
  {
    test: /must specify an aria-label or aria-labelledby/i,
    hint: 'A react-aria control (SearchField/TextField/Select/Checkbox/Slider…) on this page has no label. Give it aria-label="..." or a visible <Label>. Find it: look in the aria outline for a textbox/combobox/checkbox without a name, or check the no-accessible-name issues below/above for the exact element.',
  },
  {
    test: /Each child in a list should have a unique "key" prop|unique "key" prop/i,
    hint: 'A mapped list is missing a stable `key`. Open the file/line this message points to, find the `.map(...)` that produces this JSX, and add `key={<stable id>}` — not the array index unless the list truly never reorders or filters.',
  },
  {
    test: /Cannot update a component[\s\S]*while rendering/i,
    hint: 'A state update (setState/dispatch) is firing during render instead of inside an effect or event handler. Move the call into `useEffect` (with correct deps) or the event handler that triggers it; if you are deriving state from props, compute it inline instead of calling setState.',
  },
  {
    test: /validateDOMNesting/i,
    hint: 'Invalid HTML nesting (e.g. `<div>` inside `<p>`, a `<tr>` outside `<table>`). Open the component named in the stack and fix what it renders inside its parent element.',
  },
  {
    test: /missingKey/i,
    hint: "A t('...') call has no matching translation key. Add the key to BOTH src/shared/lib/i18n/locales/en/common.json and ru/common.json — both are required.",
  },
  {
    test: /notFoundError was encountered/i,
    hint: "TanStack Router hit a route with no matching path/loader data (a notFound() call, or a bad $param). Check the route file's loader/beforeLoad and confirm the id/path you passed to `shot` actually exists in the mock data — run `bun run shot routes` to see valid sample ids.",
  },
  {
    test: /captured a request without a matching request handler/i,
    hint: 'MSW has no mock handler for this request. Add one in src/shared/api/mocks/handlers.ts (contract endpoint) or src/shared/api/mocks/handmade/ (not in the contract yet), then register it in mocks/handlers.ts.',
  },
];

const GENERIC_CONSOLE_HINT =
  'Read the message, open the file/line it points to, fix the cause. React key/prop warnings are real bugs.';

const NODE_MODULES_NOTE =
  ' The location above is inside a library (node_modules) — the cause is in OUR component that uses it, not the library itself.';

function hintForConsoleMessage(text: string, locationIsNodeModules: boolean): string {
  const known = KNOWN_CONSOLE_HINTS.find(({ test }) => test.test(text));
  const base = known?.hint ?? GENERIC_CONSOLE_HINT;
  return locationIsNodeModules ? `${base}${NODE_MODULES_NOTE}` : base;
}

/** Подписывается на консоль/ошибки/сеть до навигации. */
export function recordPage(page: Page): PageRecorder {
  const issues: Issue[] = [];

  const onConsole = (msg: ConsoleMessage) => {
    const type = msg.type();
    if (type !== 'error' && type !== 'warning') return;
    const text = msg.text();
    if (CONSOLE_NOISE.some((re) => re.test(text))) return;
    const loc = msg.location();
    const where = loc.url ? ` @ ${loc.url.replace(/^https?:\/\/[^/]+/, '')}:${loc.lineNumber}` : '';
    const inNodeModules = loc.url?.includes('node_modules') ?? false;
    issues.push({
      severity: type === 'error' ? 'FAIL' : 'WARN',
      code: type === 'error' ? 'console-error' : 'console-warn',
      message: `${text.slice(0, 600)}${where}`,
      hint: hintForConsoleMessage(text, inNodeModules),
    });
  };
  const onPageError = (err: Error) => {
    issues.push({
      severity: 'FAIL',
      code: 'uncaught-exception',
      message: `${err.message}\n${(err.stack ?? '').split('\n').slice(0, 6).join('\n')}`,
      hint: 'Runtime crash. The stack shows the component file. Fix it before anything else.',
    });
  };
  const onRequestFailed = (req: Request) => {
    const url = req.url();
    if (url.includes('/@vite/') || url.includes('hot-update') || url.endsWith('/custom.css')) return;
    // net::ERR_ABORTED means the browser/JS cancelled the request on
    // purpose — React 19 StrictMode double-invokes effects in dev, so
    // TanStack Query's first fetch is routinely aborted (via
    // AbortController) while its retry completes fine. That's noise, not
    // a bug: a genuinely broken/missing MSW handler still surfaces as an
    // http-4xx/5xx via `onResponse` below, so nothing real is hidden.
    if (req.failure()?.errorText === 'net::ERR_ABORTED') return;
    issues.push({
      severity: 'FAIL',
      code: 'request-failed',
      message: `${req.method()} ${url} — ${req.failure()?.errorText ?? 'failed'}`,
      hint: 'A resource or API call failed. If it is /api/..., an MSW mock handler is missing (src/shared/api/mocks/).',
    });
  };
  const onResponse = (res: Response) => {
    const status = res.status();
    if (status < 400) return;
    const url = res.url();
    if (url.endsWith('/custom.css') || url.endsWith('/favicon.ico')) return;
    issues.push({
      severity: 'FAIL',
      code: `http-${status}`,
      message: `${res.request().method()} ${url} → ${status}`,
      hint: status === 404 && url.includes('/api/')
        ? 'No MSW mock for this endpoint. Add a handler in src/shared/api/mocks/handmade/ and register it in mocks/handlers.ts.'
        : 'Server returned an error. Check the mock handler or the request params.',
    });
  };

  page.on('console', onConsole);
  page.on('pageerror', onPageError);
  page.on('requestfailed', onRequestFailed);
  page.on('response', onResponse);

  return {
    issues,
    detach() {
      page.off('console', onConsole);
      page.off('pageerror', onPageError);
      page.off('requestfailed', onRequestFailed);
      page.off('response', onResponse);
    },
  };
}

interface DomFinding {
  readonly code: string;
  readonly severity: Severity;
  readonly message: string;
}

/** Выполняется в браузере. Не использовать внешние переменные. */
function domChecks(rootSelector: string): DomFinding[] {
  const out: DomFinding[] = [];
  const root = document.querySelector(rootSelector) ?? document.body;
  const vw = window.innerWidth;

  const describe = (el: Element): string => {
    const parts: string[] = [];
    let cur: Element | null = el;
    for (let i = 0; cur && i < 4; i++) {
      let s = cur.tagName.toLowerCase();
      const slot = cur.getAttribute('data-slot');
      const od = cur.getAttribute('data-od-id');
      if (od) s += `[data-od-id=${od}]`;
      else if (slot) s += `[data-slot=${slot}]`;
      else if (cur.id) s += `#${cur.id}`;
      parts.unshift(s);
      if (od) break;
      cur = cur.parentElement;
    }
    const text = (el.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 40);
    return `${parts.join(' > ')}${text ? ` "${text}"` : ''}`;
  };

  const visible = (el: Element): boolean => {
    const r = el.getBoundingClientRect();
    if (r.width === 0 || r.height === 0) return false;
    const cs = getComputedStyle(el);
    return cs.visibility !== 'hidden' && cs.display !== 'none' && cs.opacity !== '0';
  };

  // 1. Пустая страница.
  const text = (root as HTMLElement).innerText?.trim() ?? '';
  if (text.length < 5) {
    out.push({ code: 'blank', severity: 'FAIL', message: 'Page renders (almost) no text — it is blank or still loading.' });
  }

  // 2. Error boundary / not found TanStack Router.
  if (/Something went wrong!|Not Found$/m.test(text)) {
    out.push({ code: 'router-error', severity: 'FAIL', message: `Router error screen is shown: "${text.slice(0, 200)}"` });
  }

  // 3. Горизонтальный скролл всей страницы.
  const docW = document.documentElement.scrollWidth;
  if (docW > vw + 1) {
    out.push({ code: 'page-hscroll', severity: 'FAIL', message: `Page is wider than viewport: ${docW}px > ${vw}px (horizontal scrollbar).` });
  }

  // 3b. Скролл-панели, которые реально скроллят вбок, даже когда
  //     documentElement (#3) и overflow-right (#4 ниже) молчат. Пример:
  //     app-shell рендерит роуты внутри `<div overflow-auto
  //     data-od-id="app-content">`, который стоит ВЫШЕ `root` (`main`) —
  //     широкий потомок делает эту панель скроллящейся, но: (a) она не
  //     расширяет document.documentElement, потому что сама панель её
  //     скролл и поглощает; (b) overflow-right's `clippedByAncestor`
  //     как раз находит этот `overflow-x: auto` предок и считает
  //     потомка "обрезанным", а не "вылезающим" — так что сама панель,
  //     будучи снаружи `root`, никогда не проверяется. Поэтому здесь
  //     сканируем весь document, а не только root.
  const INTENDED_SCROLLER_SELECTOR =
    '[data-slot="table-container"], [data-slot="table"], .table-wrap, ' +
    '[role="grid"], [role="table"], [data-slot="tabs-list"], pre, code, ' +
    '.console, [class*="console-"], [class*="xterm"], .embla, [class*="embla"]';
  // Сам элемент — (или предок) intended-скроллер. НЕ проверяем потомков
  // через querySelector: контейнер вроде app-content почти всегда СОДЕРЖИТ
  // где-то внутри таблицу/tabs — это не делает его самого "table wrapper".
  const insideIntendedScroller = (el: Element): boolean => el.closest(INTENDED_SCROLLER_SELECTOR) !== null;

  /**
   * Спускается из контейнера к самому широкому виновнику (до maxDepth
   * уровней), игнорируя виновников, лежащих внутри intended-скроллера
   * (таблица/tabs/pre/console/embla, см. INTENDED_SCROLLER_SELECTOR).
   * Возвращает null, если ВСЕ виновники объясняются intended-скроллером —
   * значит контейнер скроллит по ожидаемой причине, репортить нечего.
   */
  const findWidestCulprit = (container: Element, maxDepth: number): Element | null => {
    const boundary = container.getBoundingClientRect().right;
    const exceeds = (el: Element): boolean =>
      visible(el) && el.getBoundingClientRect().right > boundary + 1 && !insideIntendedScroller(el);
    const subtree = Array.from(container.querySelectorAll('*')).filter(exceeds);
    const outerInSubtree = subtree.filter((el) => !subtree.some((o) => o !== el && o.contains(el)));
    if (outerInSubtree.length === 0) return null;
    let current = outerInSubtree.reduce((widest, el) =>
      el.getBoundingClientRect().right > widest.getBoundingClientRect().right ? el : widest
    );
    for (let depth = 0; depth < maxDepth; depth++) {
      let next: Element | null = null;
      for (const child of Array.from(current.children)) {
        if (!exceeds(child)) continue;
        if (!next || child.getBoundingClientRect().right > next.getBoundingClientRect().right) next = child;
      }
      if (!next) break;
      current = next;
    }
    return current;
  };

  const scrollers = Array.from(document.querySelectorAll('*')).filter((el) => {
    if (!visible(el)) return false;
    const cs = getComputedStyle(el);
    if (cs.overflowX !== 'auto' && cs.overflowX !== 'scroll') return false;
    return el.scrollWidth > el.clientWidth + 1;
  });
  // Оставляем только самые внешние (панель внутри уже отмеченной панели не дублируем).
  const outerScrollers = scrollers.filter((el) => !scrollers.some((o) => o !== el && o.contains(el)));
  for (const el of outerScrollers.slice(0, 5)) {
    if (insideIntendedScroller(el)) continue;
    const culprit = findWidestCulprit(el, 3);
    if (!culprit) continue;
    out.push({
      code: 'page-hscroll',
      severity: 'FAIL',
      message: `${describe(el)} scrolls sideways (${el.scrollWidth}px > ${el.clientWidth}px). Widest child: ${describe(culprit)}`,
    });
  }

  const all = Array.from(root.querySelectorAll('*'));

  // 4. Элементы, вылезающие за правый край окна (и не обрезанные предком).
  const clippedByAncestor = (el: Element): boolean => {
    let p = el.parentElement;
    while (p && p !== document.body) {
      const cs = getComputedStyle(p);
      if (cs.overflowX !== 'visible') return true;
      p = p.parentElement;
    }
    return false;
  };
  const overflowing = all.filter((el) => {
    if (!visible(el)) return false;
    const r = el.getBoundingClientRect();
    return r.right > vw + 2 && !clippedByAncestor(el);
  });
  // Оставляем только самые внешние.
  const outer = overflowing.filter((el) => !overflowing.some((o) => o !== el && o.contains(el)));
  for (const el of outer.slice(0, 5)) {
    const r = el.getBoundingClientRect();
    out.push({
      code: 'overflow-right',
      severity: 'FAIL',
      message: `Sticks out of the viewport by ${Math.round(r.right - vw)}px: ${describe(el)}`,
    });
  }

  // 5. Обрезанный текст без многоточия. Пропускаем sr-only/visually-hidden
  //    (1×1px, overflow:hidden — стандартный паттерн Tailwind `sr-only`
  //    для скрытых-но-озвучиваемых лейблов, например `Toggle Sidebar` в
  //    иконка-only кнопках) — это не визуальный баг, текст и не должен
  //    быть виден.
  let clipped = 0;
  for (const el of all) {
    if (clipped >= 5) break;
    if (!(el instanceof HTMLElement) || !visible(el)) continue;
    if (el.children.length > 0) continue;
    if (!el.textContent?.trim()) continue;
    const rect = el.getBoundingClientRect();
    if (rect.width <= 1 && rect.height <= 1) continue; // sr-only
    const cs = getComputedStyle(el);
    const hides = cs.overflow === 'hidden' || cs.overflowX === 'hidden' || cs.overflowX === 'clip';
    if (!hides) continue;
    if (el.scrollWidth > el.clientWidth + 1 && cs.textOverflow !== 'ellipsis') {
      clipped++;
      out.push({ code: 'text-clipped', severity: 'WARN', message: `Text is cut off without "…": ${describe(el)}` });
    }
  }

  // 6. Интерактив без доступного имени (иконочные кнопки без aria-label).
  let unnamed = 0;
  for (const el of Array.from(root.querySelectorAll('button, a[href], [role="button"]'))) {
    if (unnamed >= 5) break;
    if (!visible(el)) continue;
    const name =
      el.getAttribute('aria-label') ||
      el.getAttribute('aria-labelledby') ||
      el.getAttribute('title') ||
      (el.textContent ?? '').trim();
    if (!name) {
      unnamed++;
      out.push({ code: 'no-accessible-name', severity: 'WARN', message: `Icon-only control without aria-label: ${describe(el)}` });
    }
  }

  // 7. Непереведённые ключи i18n (текст вида "vms.detail.title").
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const keys = new Set<string>();
  for (let n = walker.nextNode(); n && keys.size < 5; n = walker.nextNode()) {
    const t = (n.textContent ?? '').trim();
    if (/^[a-z][a-zA-Z0-9]*(\.[a-zA-Z0-9_-]+){1,}$/.test(t) && !/\.(com|dev|io|net|org|ru|local)$/.test(t)) {
      keys.add(t);
    }
  }
  for (const k of keys) {
    out.push({ code: 'i18n-missing', severity: 'FAIL', message: `Raw i18n key on screen: "${k}"` });
  }

  // 8. Сломанные картинки.
  for (const img of Array.from(root.querySelectorAll('img'))) {
    if (img.complete && img.naturalWidth === 0 && visible(img)) {
      out.push({ code: 'broken-image', severity: 'WARN', message: `Image failed to load: ${img.getAttribute('src')}` });
    }
  }

  // 9. Form/aria widgets without an accessible name — catches the
  //    react-aria "must specify an aria-label or aria-labelledby" console
  //    warning at the DOM level, so the report names the exact element
  //    (`describe(el)`) instead of just repeating the console message.
  //    Only counts the same three sources react-aria itself recognizes:
  //    aria-label, a RESOLVED aria-labelledby (points at an id with text),
  //    or a <label for> pointing at this element's id.
  let namelessWidgets = 0;
  const widgetSelector =
    'input, [role="combobox"], [role="slider"], [role="checkbox"], [role="switch"], [role="searchbox"], [role="textbox"]';
  for (const el of Array.from(root.querySelectorAll(widgetSelector))) {
    if (namelessWidgets >= 5) break;
    if (!visible(el)) continue;
    if (el instanceof HTMLInputElement && el.type === 'hidden') continue;
    const ariaLabel = el.getAttribute('aria-label');
    const labelledBy = el.getAttribute('aria-labelledby');
    const labelledByResolved = labelledBy
      ? labelledBy.split(/\s+/).some((id) => (document.getElementById(id)?.textContent ?? '').trim().length > 0)
      : false;
    const labelFor = el.id ? document.querySelector(`label[for="${el.id}"]`) : null;
    const hasName = Boolean(ariaLabel) || labelledByResolved || Boolean((labelFor?.textContent ?? '').trim());
    if (!hasName) {
      namelessWidgets++;
      out.push({
        code: 'no-accessible-name',
        severity: 'WARN',
        message: `Control has no accessible name (no aria-label / resolved aria-labelledby / <label for>): ${describe(el)}`,
      });
    }
  }

  return out;
}

const DOM_HINTS: Record<string, string> = {
  blank: 'Check uncaught exceptions above; if none — the component returns null or data never arrives (mock missing?).',
  'router-error': 'Route crashed or path is wrong. Check src/routes/<file>.tsx and createFileRoute path string.',
  'page-hscroll': 'Something is too wide — check the "Widest child" named in the message for a fixed `w-[...px]`, a `min-w-*`, or a flex child missing `min-w-0`. See overflow-right items too; an `overflow-x-auto` wrapper is expected (and fine) for tables.',
  'overflow-right': 'Add min-w-0 / truncate / flex-wrap, or remove fixed widths (w-[...px]).',
  'text-clipped': 'Add `truncate` (gives "…") or allow wrapping; for IDs use <CopyableText>.',
  'no-accessible-name': 'Add aria-label="..." (or a visible <Label htmlFor="...">) to this control. Icon-only buttons and unlabeled form widgets (SearchField/Select/Checkbox/Slider/etc. from react-aria-components) both need an accessible name.',
  'i18n-missing': 'Add the key to the locale file (grep the key prefix in src/shared/lib/i18n) or fix the t("...") call.',
  'broken-image': 'Fix the src path or remove the image.',
};

export async function inspectDom(page: Page, rootSelector: string): Promise<Issue[]> {
  const found = await page.evaluate(domChecks, rootSelector);
  return found.map((f) => ({ ...f, hint: DOM_HINTS[f.code] ?? '' }));
}

/** innerText корня, обрезанный до maxLines — «видимый текст» для отчёта. */
export async function visibleText(page: Page, rootSelector: string, maxLines = 80): Promise<string> {
  try {
    const text = await page.locator(rootSelector).first().innerText({ timeout: 5_000 });
    const lines = text.split('\n');
    if (lines.length <= maxLines) return text;
    return `${lines.slice(0, maxLines).join('\n')}\n… (${lines.length - maxLines} more lines cut)`;
  } catch (err) {
    return `(innerText unavailable: ${(err as Error).message.split('\n')[0]})`;
  }
}

/** Текстовая структура страницы (роли + имена). Обрезается до maxLines. */
export async function ariaOutline(page: Page, rootSelector: string, maxLines = 150): Promise<string> {
  try {
    const loc = page.locator(rootSelector).first();
    const snap = await loc.ariaSnapshot({ timeout: 5_000 });
    const lines = snap.split('\n');
    if (lines.length <= maxLines) return snap;
    return `${lines.slice(0, maxLines).join('\n')}\n… (${lines.length - maxLines} more lines cut)`;
  } catch (err) {
    return `(aria snapshot unavailable: ${(err as Error).message.split('\n')[0]})`;
  }
}
