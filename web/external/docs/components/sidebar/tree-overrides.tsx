'use client';

import { usePathname } from 'fumadocs-core/framework';
import { useTreeContext } from 'fumadocs-ui/contexts/tree';
import {
  SidebarFolder,
  SidebarFolderContent,
  SidebarFolderLink,
  SidebarFolderTrigger,
  SidebarItem,
  useFolderDepth,
} from 'fumadocs-ui/components/sidebar/base';
import type { SidebarPageTreeComponents } from 'fumadocs-ui/components/sidebar/page-tree';
import { useActiveAncestors } from '@/components/sidebar/use-active-ancestors';
import { ChapterLabel, useChapterIndex } from '@/components/sidebar/chapter-label';

/**
 * Local copy of fumadocs-ui's `isActive(href, pathname, nested)`. The
 * upstream helper (`fumadocs-ui/dist/utils/urls.js`) ships without a
 * `.d.ts`, so we inline it here to avoid a TypeScript module-not-found
 * error at build time.
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
 * Plexor-styled page-tree overrides for the docs sidebar.
 *
 * The defaults shipped by `fumadocs-ui/layouts/docs/slots/sidebar.js`
 * use a bg-fill at depth 0 and a 1px left-rule at depth >= 1. Plexor
 * wants the left-rule at every depth for a more architectural feel
 * (see console's `app-sidebar.tsx` which uses a 2px rule on the active
 * item). The overrides below also force-expand the folders that
 * contain the active page, so users who land deep in the tree (e.g.
 * `/en/storage/snapshots`) see the path unfolded without writing
 * `defaultOpen: true` into every `meta.json`.
 *
 * `Separator` is kept close to the default look — the meta.json
 * separators (`---Plexor---` etc.) are still rendered as small
 * uppercase labels above each section.
 */
export const SidebarOverrides: Partial<SidebarPageTreeComponents> = {
  Item: PlexorSidebarItem,
  Folder: PlexorSidebarFolder,
  Separator: PlexorSidebarSeparator,
};

function PlexorSidebarItem(
  props: Parameters<NonNullable<SidebarPageTreeComponents['Item']>>[0],
) {
  const pathname = usePathname();
  const depth = useFolderDepth();
  // Exact-match active state, matching fumadocs's default (`nested=false`).
  // Ancestor highlighting is handled by the Folder override via
  // `useActiveAncestors` + `defaultOpen`, NOT by marking ancestor items
  // active — the storage/snapshots page should only highlight the
  // snapshots item, not the storage index.
  const active = isActive(props.item.url, pathname);
  return (
    <SidebarItem
      href={props.item.url}
      external={props.item.external}
      active={active}
      icon={props.item.icon}
      className={plexorItemClasses(active)}
      style={{ paddingInlineStart: itemOffset(depth) }}
    >
      {props.item.name}
    </SidebarItem>
  );
}

function PlexorSidebarFolder(
  props: Parameters<NonNullable<SidebarPageTreeComponents['Folder']>>[0],
) {
  const { full } = useTreeContext();
  const pathname = usePathname();
  const depth = useFolderDepth();
  const ancestors = useActiveAncestors(full);
  const folderUrl = props.item.index?.url;
  const active = folderUrl !== undefined && ancestors.has(folderUrl);
  const indexActive =
    folderUrl !== undefined && isActive(folderUrl, pathname, true);

  return (
    <SidebarFolder
      collapsible={props.item.collapsible}
      active={active}
      defaultOpen={active || props.item.defaultOpen}
      className="plexor-sidebar-folder"
    >
      {props.item.index ? (
        <SidebarFolderLink
          href={props.item.index.url}
          external={props.item.index.external}
          active={indexActive}
          className={plexorFolderClasses(indexActive)}
          style={{ paddingInlineStart: itemOffset(depth) }}
        >
          {props.item.icon}
          {props.item.name}
        </SidebarFolderLink>
      ) : (
        <SidebarFolderTrigger
          className={plexorFolderClasses(active)}
          style={{ paddingInlineStart: itemOffset(depth) }}
        >
          {props.item.icon}
          {props.item.name}
        </SidebarFolderTrigger>
      )}
      <SidebarFolderContent className="plexor-sidebar-folder-content">
        {props.children}
      </SidebarFolderContent>
    </SidebarFolder>
  );
}

function PlexorSidebarSeparator(
  props: Parameters<NonNullable<SidebarPageTreeComponents['Separator']>>[0],
) {
  // `useChapterIndex()` is called from a component body so the rules of
  // hooks are satisfied — the counter lives on a `useRef` singleton
  // owned by the module-scoped provider below.
  const index = useChapterIndex();
  return (
    <ChapterLabel index={index} title={props.item.name} />
  );
}

function plexorItemClasses(active: boolean): string {
  return [
    'relative flex flex-row items-center gap-2 rounded-lg p-2 text-start wrap-anywhere',
    '[&_svg]:size-4 [&_svg]:shrink-0',
    'transition-colors',
    active
      ? 'text-fd-primary font-medium data-[active=true]:before:content-[\'\'] data-[active=true]:before:bg-fd-primary data-[active=true]:before:absolute data-[active=true]:before:w-0.5 data-[active=true]:before:inset-y-2.5 data-[active=true]:before:inset-s-2.5'
      : 'text-fd-muted-foreground hover:bg-fd-accent/50 hover:text-fd-accent-foreground/80',
  ].join(' ');
}

function plexorFolderClasses(active: boolean): string {
  return [
    'relative flex w-full flex-row items-center gap-2 rounded-lg p-2 text-start wrap-anywhere',
    '[&_svg]:size-4 [&_svg]:shrink-0',
    'transition-colors',
    active
      ? 'text-fd-primary font-medium data-[active=true]:before:content-[\'\'] data-[active=true]:before:bg-fd-primary data-[active=true]:before:absolute data-[active=true]:before:w-0.5 data-[active=true]:before:inset-y-2.5 data-[active=true]:before:inset-s-2.5'
      : 'text-fd-muted-foreground hover:bg-fd-accent/50 hover:text-fd-accent-foreground/80',
  ].join(' ');
}

/**
 * Same offset curve as `fumadocs-ui/layouts/docs/slots/sidebar.js`
 * `getItemOffset(depth)`. We import the base `SidebarItem` /
 * `SidebarFolderLink` / `SidebarFolderTrigger` directly instead of the
 * docs-layout wrappers, so we have to apply the indent ourselves —
 * otherwise items sit flush against the left edge instead of stepping
 * in by 8px / 20px / 32px as the depth grows.
 */
function itemOffset(depth: number): string {
  return `calc(${2 + 3 * depth} * var(--spacing))`;
}