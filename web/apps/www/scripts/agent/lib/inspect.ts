/**
 * Checks against a rendered page — the agent's text-shaped "eyes".
 *
 * The model may not be able to look at a PNG, so everything a human
 * would see gets duplicated as text: console errors, failed requests,
 * horizontal scroll, clipped text, unlabeled buttons, plus an aria
 * snapshot of the page structure.
 *
 * Adapted from `web/apps/console/scripts/agent/lib/inspect.ts`: the
 * known-console-message hints for i18next/MSW (console-specific
 * subsystems `www` doesn't have) were dropped; everything else — the DOM
 * checks (blank page, router error, horizontal scroll, overflow,
 * clipped text, unnamed controls, broken images) — is unchanged and
 * applies just as well to a static MDX/TanStack Router site.
 */

import type { ConsoleMessage, Page, Request, Response } from 'playwright';

export type Severity = 'FAIL' | 'WARN';

export interface Issue {
  readonly severity: Severity;
  readonly code: string;
  readonly message: string;
  readonly hint: string;
}

/** Noise that isn't a page problem. */
const CONSOLE_NOISE = [
  /Download the React DevTools/i,
  /\[vite\]/i,
  /Lit is in dev mode/i,
];

export interface PageRecorder {
  readonly issues: Issue[];
  detach(): void;
}

/**
 * Known console messages with an actionable hint, matched against the
 * raw message text. Checked in order, first match wins. Add a row here
 * whenever the same warning keeps showing up across pages with only the
 * generic "read the message" fallback.
 */
const KNOWN_CONSOLE_HINTS: ReadonlyArray<{ readonly test: RegExp; readonly hint: string }> = [
  {
    test: /must specify an aria-label or aria-labelledby/i,
    hint: 'A react-aria control (Input/Dialog/Menu/…) on this page has no label. Give it aria-label="..." or a visible label. Find it in the aria outline below — a textbox/combobox/checkbox without a name.',
  },
  {
    test: /Each child in a list should have a unique "key" prop|unique "key" prop/i,
    hint: 'A mapped list is missing a stable `key`. Open the file/line this message points to, find the `.map(...)` that produces this JSX, and add `key={<stable id>}` — not the array index unless the list truly never reorders or filters.',
  },
  {
    test: /Cannot update a component[\s\S]*while rendering/i,
    hint: 'A state update (setState/dispatch) is firing during render instead of inside an effect or event handler. Move the call into `useEffect` (with correct deps) or the event handler that triggers it.',
  },
  {
    test: /validateDOMNesting/i,
    hint: 'Invalid HTML nesting (e.g. `<div>` inside `<p>`, a `<tr>` outside `<table>`). Open the component named in the stack and fix what it renders inside its parent element.',
  },
  {
    test: /notFoundError was encountered/i,
    hint: "TanStack Router hit a route with no matching path (a notFound() call, or a bad path). Run `bun run shot routes` to see the real path list.",
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

/** Subscribes to console/errors/network BEFORE navigation. */
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
    // net::ERR_ABORTED usually means the browser/JS cancelled the request
    // on purpose (React 19 StrictMode double-invoking effects in dev) — not a bug.
    if (req.failure()?.errorText === 'net::ERR_ABORTED') return;
    issues.push({
      severity: 'FAIL',
      code: 'request-failed',
      message: `${req.method()} ${url} — ${req.failure()?.errorText ?? 'failed'}`,
      hint: 'A resource or navigation request failed. Check the URL and that the asset actually exists in the built/served output.',
    });
  };
  const onResponse = (res: Response) => {
    const status = res.status();
    if (status < 400) return;
    const url = res.url();
    if (url.endsWith('/custom.css') || url.endsWith('/favicon.ico') || url.endsWith('/favicon.svg')) return;
    issues.push({
      severity: 'FAIL',
      code: `http-${status}`,
      message: `${res.request().method()} ${url} → ${status}`,
      hint: 'Server returned an error for this request — check the URL/asset path.',
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

/** Runs in the browser. Don't reference outside variables. */
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
      if (slot) s += `[data-slot=${slot}]`;
      else if (cur.id) s += `#${cur.id}`;
      parts.unshift(s);
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

  // 1. Blank page.
  const text = (root as HTMLElement).innerText?.trim() ?? '';
  if (text.length < 5) {
    out.push({ code: 'blank', severity: 'FAIL', message: 'Page renders (almost) no text — it is blank or still loading.' });
  }

  // 2. Error boundary / router not-found.
  if (/Something went wrong!|Not Found$/m.test(text)) {
    out.push({ code: 'router-error', severity: 'FAIL', message: `Router error screen is shown: "${text.slice(0, 200)}"` });
  }

  // 3. Whole-page horizontal scroll.
  const docW = document.documentElement.scrollWidth;
  if (docW > vw + 1) {
    out.push({ code: 'page-hscroll', severity: 'FAIL', message: `Page is wider than viewport: ${docW}px > ${vw}px (horizontal scrollbar).` });
  }

  // 3b. Panels that scroll sideways even when #3/#4 stay quiet — scan the
  // whole document, not just root, so an app-shell-style overflow wrapper
  // above root is still caught.
  const INTENDED_SCROLLER_SELECTOR =
    '[data-slot="table-container"], [data-slot="table"], .table-wrap, ' +
    '[role="grid"], [role="table"], [data-slot="tabs-list"], pre, code';
  const insideIntendedScroller = (el: Element): boolean => el.closest(INTENDED_SCROLLER_SELECTOR) !== null;

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

  // 4. Elements sticking out past the right edge (and not clipped by an ancestor).
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
  const outer = overflowing.filter((el) => !overflowing.some((o) => o !== el && o.contains(el)));
  for (const el of outer.slice(0, 5)) {
    const r = el.getBoundingClientRect();
    out.push({
      code: 'overflow-right',
      severity: 'FAIL',
      message: `Sticks out of the viewport by ${Math.round(r.right - vw)}px: ${describe(el)}`,
    });
  }

  // 5. Clipped text without an ellipsis. Skip sr-only (1x1px, overflow hidden) — not a bug.
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

  // 6. Interactive elements with no accessible name (icon-only buttons/links without aria-label).
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

  // 7. Leftover placeholder/key-shaped text on screen (e.g. a raw "section.title" string).
  // Excludes version numbers ("v0.2", "1.4.0") — plexor's own UI legitimately
  // shows these everywhere (header badge, changelog, roadmap/spectrum) and
  // they match the same dotted-lowercase shape as a leaked i18n key.
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const keys = new Set<string>();
  for (let n = walker.nextNode(); n && keys.size < 5; n = walker.nextNode()) {
    const t = (n.textContent ?? '').trim();
    if (/^v?\d+(\.\d+)+$/i.test(t)) continue;
    if (/^[a-z][a-zA-Z0-9]*(\.[a-zA-Z0-9_-]+){1,}$/.test(t) && !/\.(com|dev|io|net|org|local)$/.test(t)) {
      keys.add(t);
    }
  }
  for (const k of keys) {
    out.push({ code: 'raw-key-text', severity: 'FAIL', message: `Text on screen looks like a leftover key/placeholder, not real copy: "${k}"` });
  }

  // 8. Broken images.
  for (const img of Array.from(root.querySelectorAll('img'))) {
    if (img.complete && img.naturalWidth === 0 && visible(img)) {
      out.push({ code: 'broken-image', severity: 'WARN', message: `Image failed to load: ${img.getAttribute('src')}` });
    }
  }

  // 9. Form/aria widgets without an accessible name.
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
  blank: 'Check uncaught exceptions above; if none — the component returns null or data never arrived.',
  'router-error': 'Route crashed or the path is wrong. Check src/routes/<file>.tsx and its createFileRoute path string.',
  'page-hscroll': 'Something is too wide — check the "Widest child" named in the message for a fixed `w-[...px]`, a `min-w-*`, or a flex child missing `min-w-0`.',
  'overflow-right': 'Add min-w-0 / truncate / flex-wrap, or remove fixed widths (w-[...px]).',
  'text-clipped': 'Add `truncate` (gives "…") or allow wrapping.',
  'no-accessible-name': 'Add aria-label="..." (or a visible label) to this control.',
  'raw-key-text': 'Replace the placeholder with real copy, or fix the prop/binding that produced it.',
  'broken-image': 'Fix the src path or remove the image.',
};

export async function inspectDom(page: Page, rootSelector: string): Promise<Issue[]> {
  const found = await page.evaluate(domChecks, rootSelector);
  return found.map((f) => ({ ...f, hint: DOM_HINTS[f.code] ?? '' }));
}

/** Root's innerText, truncated to maxLines — the "visible text" section of the report. */
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

/** Text structure of the page (roles + names), truncated to maxLines. */
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
