'use client';

import { useMemo } from 'react';
import { usePathname } from 'fumadocs-core/framework';
import type * as PageTree from 'fumadocs-core/page-tree';

/**
 * Local copy of fumadocs-ui's `isActive(href, pathname, nested)`. The
 * upstream helper lives at `fumadocs-ui/dist/utils/urls.js` but isn't
 * shipped with a `.d.ts` (it's an internal-only file in the package),
 * so importing it here trips `tsc`'s `Cannot find module` error even
 * though the runtime resolves fine. Re-implementing it is 4 lines and
 * avoids pulling an undeclared path into our dependency surface.
 */
function isActive(href: string, pathname: string, nested = false): boolean {
  const normalizedHref = href.length > 1 && href.endsWith('/') ? href.slice(0, -1) : href;
  const normalizedPath =
    pathname.length > 1 && pathname.endsWith('/') ? pathname.slice(0, -1) : pathname;
  return (
    normalizedHref === normalizedPath ||
    (nested && normalizedPath.startsWith(`${normalizedHref}/`))
  );
}

/**
 * Walks a page tree and returns the Set of folder URLs whose subtree
 * contains the current pathname.
 *
 * Each folder URL in the returned Set points at the folder's `index`
 * page (folders don't carry URLs directly — the convention in
 * fumadocs is that `folder.index.url` is the folder's addressable URL
 * when it exists; when a folder has no index, it can't be force-opened
 * via URL anyway, so it's not interesting for the active-ancestor
 * check).
 *
 * Uses `isActive(..., nested=true)` semantics: a folder is "active" if
 * its URL is exactly the pathname OR a strict prefix of it (so
 * `https://docs/foo/` activates `https://docs/foo` AND `https://docs/`
 * but NOT `https://docs/foobar/`).
 *
 * Returned as a Set of strings (URLs), not of nodes, so callers can
 * pass it into the render context without holding node references
 * across renders (nodes may be regenerated on each tree reload).
 */
export function useActiveAncestors(tree: PageTree.Root): Set<string> {
  const pathname = usePathname();
  return useMemo(() => collectActiveFolderUrls(tree, pathname), [tree, pathname]);
}

function collectActiveFolderUrls(root: PageTree.Root, pathname: string): Set<string> {
  const result = new Set<string>();
  walk(root.children, pathname, result);
  return result;
}

function walk(nodes: PageTree.Node[], pathname: string, result: Set<string>): void {
  for (const node of nodes) {
    if (node.type === 'folder') {
      const folderUrl = node.index?.url;
      if (folderUrl !== undefined && isActive(folderUrl, pathname, true)) {
        result.add(folderUrl);
      }
      walk(node.children, pathname, result);
    }
  }
}