import * as React from "react"

/**
 * Flatten a `DropdownMenu`/`DropdownMenuContent` children list, unwrapping
 * transparent wrappers (`<>...</>`, `DropdownMenuGroup`) so a
 * `DropdownMenuLabel` / `DropdownMenuSeparator` / `DropdownMenuTrigger`
 * nested inside one of those is still discoverable by its `displayName`.
 *
 * Shared by `DropdownMenu` (root — looks for the trigger among its
 * children) and `DropdownMenuContent` (looks for meta vs. item children).
 * Split out of the former single `dropdown-menu.tsx` so both call sites
 * import one definition instead of duplicating it (file-length cap split,
 * see `dropdown-menu.tsx`'s barrel comment).
 */
export function flattenChildren(children: React.ReactNode): React.ReactElement[] {
  const out: React.ReactElement[] = []
  React.Children.forEach(children, (c) => {
    if (!React.isValidElement(c)) return
    const display = (c.type as { displayName?: string })?.displayName
    if (display === "PlexorDropdownMenuGroup" || c.type === React.Fragment) {
      const groupChildren = (c.props as { children?: React.ReactNode }).children
      out.push(...flattenChildren(groupChildren))
      return
    }
    out.push(c)
  })
  return out
}
