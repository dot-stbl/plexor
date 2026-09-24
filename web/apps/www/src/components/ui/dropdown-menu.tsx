/**
 * DropdownMenu — Plexor DS wrapper around react-aria-components' Menu.
 * Ported from
 * `web/apps/console/src/shared/ui/primitives/dropdown-menu.tsx`
 * (the `@/lib/utils` import path was adjusted; the file was then split
 * across several cohesive modules to stay under the project's 200-LOC
 * cap — this file is the public barrel, the import path
 * `@/components/ui/dropdown-menu` is unchanged for every caller).
 *
 * Compatibility shims vs base-ui:
 *   - `<DropdownMenuTrigger render={<Button>}>`: clones the render target
 *     with our trigger className merged in (`dropdown-menu-trigger.tsx`).
 *   - `<DropdownMenuItem>` accepts `onClick` (HTML) → RAC `onAction`
 *     (`dropdown-menu-item.tsx`).
 *   - `<DropdownMenuItem inset variant="destructive">` map to data attrs
 *     via the wrapper's className.
 *   - Submenu: kept as a thin wrapper over `<Menu>` (RAC requires a
 *     `<SubmenuTrigger>` inside a parent `<Menu>`; we make this work
 *     structurally) — `dropdown-menu-sub.tsx`.
 *
 * File map:
 *   - `dropdown-menu-shared.ts`   — `flattenChildren` (root + content share it)
 *   - `dropdown-menu-root.tsx`    — `DropdownMenu`, `DropdownMenuPortal`
 *   - `dropdown-menu-trigger.tsx` — `DropdownMenuTrigger`
 *   - `dropdown-menu-content.tsx` — `DropdownMenuContent`
 *   - `dropdown-menu-item.tsx`    — Item/Group/Label/Separator/Shortcut/
 *                                    CheckboxItem/RadioGroup/RadioItem
 *   - `dropdown-menu-sub.tsx`     — Sub/SubTrigger/SubContent
 */

export { DropdownMenu, DropdownMenuPortal } from "./dropdown-menu-root"
export { DropdownMenuTrigger } from "./dropdown-menu-trigger"
export { DropdownMenuContent } from "./dropdown-menu-content"
export {
  DropdownMenuItem,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuCheckboxItem,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuShortcut,
} from "./dropdown-menu-item"
export {
  DropdownMenuSub,
  DropdownMenuSubTrigger,
  DropdownMenuSubContent,
} from "./dropdown-menu-sub"
