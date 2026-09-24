"use client"

import * as React from "react"
import { Menu as RACMenu, Popover } from "react-aria-components"

import { cn } from "@/lib/utils"
import { flattenChildren } from "./dropdown-menu-shared"

interface DropdownMenuContentProps extends React.HTMLAttributes<HTMLDivElement> {
  align?: "start" | "center" | "end"
  alignOffset?: number
  side?: "top" | "right" | "bottom" | "left"
  sideOffset?: number
  children?: React.ReactNode
  className?: string
}

/**
 * Render an inner DropdownMenuContent's children:
 * - DropdownMenuItem / DropdownMenuCheckboxItem / DropdownMenuRadioItem → wrapped in RACMenuItem
 * - DropdownMenuSeparator → plain divider inside Menu
 * - DropdownMenuLabel / DropdownMenuGroup → plain header/section
 *
 * RAC's Menu only honours MenuItem descendants for keyboard nav, but it
 * also renders other children literally between menu items, so we can
 * place Separator/Label as siblings of MenuItem without breaking layout.
 */
export function DropdownMenuContent({ className, children, side = "bottom", sideOffset = 4, align = "start" }: DropdownMenuContentProps) {
  // Render Separator/Label **outside** RACMenu (as siblings in the popover),
  // and items **inside** RACMenu. RAC's Menu strips unknown children even
  // when they're real primitives, so mixing them at the Menu level would
  // hide them.
  const arr = flattenChildren(children)
  const menuItems: React.ReactNode[] = []
  const meta: React.ReactNode[] = []
  for (let i = 0; i < arr.length; i++) {
    const c = arr[i]!
    const display = (c.type as { displayName?: string })?.displayName
    if (display === "PlexorDropdownMenuSeparator" || display === "PlexorDropdownMenuLabel") {
      meta.push(c)
    } else {
      menuItems.push(c)
    }
  }
  return (
    <Popover
      data-slot="dropdown-menu-content"
      placement={side}
      offset={sideOffset}
      {...(align ? { align } : {})}
      className={cn(
        "z-popover max-h-(--available-height) w-(--anchor-width) min-w-32 origin-(--transform-origin) overflow-x-hidden overflow-y-auto rounded-lg bg-popover p-1 text-popover-foreground shadow-md ring-1 ring-foreground/10 duration-100 outline-none",
        "data-[side=bottom]:slide-in-from-top-2 data-[side=inline-end]:slide-in-from-left-2 data-[side=inline-start]:slide-in-from-right-2 data-[side=left]:slide-in-from-right-2 data-[side=top]:slide-in-from-bottom-2",
        "data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95",
        "data-exiting:animate-out data-exiting:overflow-hidden data-exiting:fade-out-0 data-exiting:zoom-out-95",
        className
      )}
    >
      {meta}
      <RACMenu>{menuItems}</RACMenu>
    </Popover>
  )
}
