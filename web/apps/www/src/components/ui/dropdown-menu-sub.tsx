"use client"

import * as React from "react"
import { KeyboardArrowRight } from "@nine-thirty-five/material-symbols-react/rounded/700"
import { Popover } from "react-aria-components"

import { cn } from "@/lib/utils"

/**
 * Submenu — RAC's `<SubmenuTrigger>` must be inside a parent `<Menu>`.
 * For the legacy base-ui-style API where submenus are declarative siblings,
 * we render a Trigger with KeyboardArrowRight arrow next to children.
 * Limitations: nesting beyond one level is not supported in this version.
 */
interface DropdownMenuSubTriggerProps extends React.HTMLAttributes<HTMLDivElement> {
  className?: string
  inset?: boolean
  children?: React.ReactNode
}

export function DropdownMenuSubTrigger({ className, inset, children, ...props }: DropdownMenuSubTriggerProps) {
  return (
    <div
      data-slot="dropdown-menu-sub-trigger"
      data-inset={inset}
      className={cn(
        "flex min-h-7 cursor-default items-center gap-2 rounded-md px-2 py-1 text-xs outline-hidden select-none",
        "data-popup-open:bg-accent data-popup-open:text-accent-foreground data-open:bg-accent data-open:text-accent-foreground",
        "[&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-3.5",
        "data-inset:pl-7.5",
        className
      )}
      {...props}
    >
      {children}
      <KeyboardArrowRight strokeWidth={2} className="ml-auto" />
    </div>
  )
}

interface DropdownMenuSubContentProps {
  className?: string
  children?: React.ReactNode
}

export function DropdownMenuSubContent({ className, children }: DropdownMenuSubContentProps) {
  return (
    <Popover
      data-slot="dropdown-menu-sub-content"
      className={cn(
        "z-popover w-auto min-w-32 rounded-lg bg-popover p-1 text-popover-foreground shadow-md ring-1 ring-foreground/10 duration-100",
        "data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
        "data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95",
        "data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95",
        className
      )}
    >
      {children}
    </Popover>
  )
}

export function DropdownMenuSub({ children }: { children?: React.ReactNode }) {
  void children
  return null
}
