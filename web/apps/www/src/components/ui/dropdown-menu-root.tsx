"use client"

import * as React from "react"
import { MenuTrigger } from "react-aria-components"

import { flattenChildren } from "./dropdown-menu-shared"

/**
 * Wrapper: <DropdownMenu>{trigger, content, ...}</DropdownMenu>. RAC's
 * MenuTrigger expects both the trigger element and a `<Menu>` component as
 * siblings. We split the children and wrap with MenuTrigger. Fragments
 * and DropdownMenuGroup wrappers are flattened so callers can wrap
 * siblings in `<>...</>` or `<DropdownMenuGroup>...</DropdownMenuGroup>`.
 */
export function DropdownMenu({ children }: { children?: React.ReactNode }) {
  const arr = flattenChildren(children)
  const trigger = arr.find(
    (c) => (c.type as { displayName?: string })?.displayName === "PlexorDropdownMenuTrigger"
  )
  const others = arr.filter(
    (c) => (c.type as { displayName?: string })?.displayName !== "PlexorDropdownMenuTrigger"
  )
  if (!trigger) {
    return <>{children}</>
  }
  return (
    <MenuTrigger>
      {trigger}
      {others}
    </MenuTrigger>
  )
}

/** Compatibility: base-ui DropdownMenuPortal — RAC doesn't have portals in the same way. */
export function DropdownMenuPortal({ children }: { children?: React.ReactNode }) {
  return <>{children}</>
}
