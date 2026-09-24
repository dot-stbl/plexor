import { isValidElement, type ReactNode } from 'react';

/**
 * Recovers the plain-text contents of an MDX-compiled `<pre><code>…</code>
 * </pre>` tree, so the code-block copy button (`mdx-components.tsx`'s
 * `pre`) can copy the raw source rather than "[object Object]". The docs
 * pipeline has no syntax-highlighter plugin, so a fenced code block always
 * compiles to a single `<code>` element with a plain string child — this
 * still walks arrays/nested elements defensively in case that ever changes.
 */
export function extractCodeText(node: ReactNode): string {
  if (typeof node === 'string') return node;
  if (typeof node === 'number') return String(node);
  if (Array.isArray(node)) return node.map(extractCodeText).join('');
  if (isValidElement<{ children?: ReactNode }>(node)) {
    return extractCodeText(node.props.children ?? null);
  }
  return '';
}
