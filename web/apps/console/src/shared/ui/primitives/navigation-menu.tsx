import * as React from "react"
import { KeyboardArrowDown } from '@nine-thirty-five/material-symbols-react/rounded/700';
import {
  Button as RACButton,
  Link as RACLink,
  MenuTrigger,
  Popover,
} from "react-aria-components"
import { cva } from "class-variance-authority"

import { cn } from "@/lib/utils"

/**
 * NavigationMenu — react-aria-components composition (base-ui compat).
 *
 * RAC has no dedicated navigation-menu primitive (no shared viewport
 * animation). Composition chosen here:
 *   - NavigationMenuItem is the dispatcher (mirrors DropdownMenu): it
 *     pairs its [Trigger, Content] children into a RAC MenuTrigger, so
 *     the base-ui declarative structure keeps working.
 *   - NavigationMenuContent renders a RAC Popover; links inside are
 *     plain RAC Links navigated with Tab (menu-style arrow-key
 *     collection nav is a Menu-only concept and mega-menus here are
 *     link grids, not action menus).
 *   - NavigationMenuPositioner / NavigationMenuIndicator / the embedded
 *     global viewport of base-ui have no RAC equivalent: Positioner is
 *     a passthrough, Indicator renders its static markup (hidden).
 */

interface NavigationMenuRootProps {
  align?: "start" | "center" | "end"
  className?: string
  children?: React.ReactNode
}

function NavigationMenu({ className, children, ...props }: NavigationMenuRootProps) {
  return (
    <nav
      data-slot="navigation-menu"
      className={cn(
        "group/navigation-menu relative flex max-w-max flex-1 items-center justify-center",
        className
      )}
      {...props}
    >
      {children}
    </nav>
  )
}

function NavigationMenuList({ className, ...props }: React.ComponentProps<"ul">) {
  return (
    <ul
      data-slot="navigation-menu-list"
      className={cn(
        "group flex flex-1 list-none items-center justify-center gap-0",
        className
      )}
      {...props}
    />
  )
}

function flattenChildren(children: React.ReactNode): React.ReactElement[] {
  const out: React.ReactElement[] = []
  React.Children.forEach(children, (child) => {
    if (!React.isValidElement(child)) {
      return
    }
    if (child.type === React.Fragment) {
      const inner = (child.props as { children?: React.ReactNode }).children
      out.push(...flattenChildren(inner))
      return
    }
    out.push(child)
  })
  return out
}

function NavigationMenuItem({ className, ...props }: React.ComponentProps<"li">) {
  const arr = flattenChildren(props.children)
  const trigger = arr.find(
    (child) => (child.type as { displayName?: string })?.displayName === "PlexorNavigationMenuTrigger",
  )
  const content = arr.filter(
    (child) => (child.type as { displayName?: string })?.displayName !== "PlexorNavigationMenuTrigger",
  )

  return (
    <li
      data-slot="navigation-menu-item"
      className={cn("relative", className)}
      {...props}
    >
      {trigger != null ? (
        <MenuTrigger>
          {trigger}
          {content}
        </MenuTrigger>
      ) : (
        content
      )}
    </li>
  )
}

const navigationMenuTriggerStyle = cva(
  "group/navigation-menu-trigger inline-flex h-9 w-max items-center justify-center rounded-lg px-2.5 py-1.5 text-xs/relaxed font-medium transition-all outline-none hover:bg-muted focus:bg-muted focus-visible:ring-2 focus-visible:ring-ring/30 focus-visible:outline-1 disabled:pointer-events-none disabled:opacity-50 aria-expanded:bg-muted/50 aria-expanded:hover:bg-muted data-popup-open:bg-muted/50 data-open:bg-muted/50"
)

function NavigationMenuTrigger({
  className,
  children,
  ...props
}: React.ComponentProps<typeof RACButton>) {
  return (
    <RACButton
      data-slot="navigation-menu-trigger"
      className={cn(navigationMenuTriggerStyle(), "group", className)}
      {...props}
    >
      {children as React.ReactNode}{" "}
      <KeyboardArrowDown className="relative top-px ml-1 size-3 transition duration-300 group-aria-expanded/navigation-menu-trigger:rotate-180 group-data-open/navigation-menu-trigger:rotate-180" aria-hidden="true" />
    </RACButton>
  )
}
NavigationMenuTrigger.displayName = "PlexorNavigationMenuTrigger"

function NavigationMenuContent({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <Popover
      data-slot="navigation-menu-content"
      className={cn(
        "z-50 w-auto rounded-xl bg-popover p-1.5 text-popover-foreground shadow-md ring-1 ring-foreground/10 duration-300 outline-none",
        "data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95 data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95",
        "**:data-[slot=navigation-menu-link]:focus:ring-0 **:data-[slot=navigation-menu-link]:focus:outline-none",
        className
      )}
      {...props}
    />
  )
}

/** base-ui compat passthrough — positioning is owned by the RAC Popover. */
function NavigationMenuPositioner({ children, ...props }: React.ComponentProps<"div">) {
  return (
    <div data-slot="navigation-menu-positioner" {...props}>
      {children}
    </div>
  )
}

function NavigationMenuLink({
  className,
  ...props
}: React.ComponentProps<typeof RACLink>) {
  return (
    <RACLink
      data-slot="navigation-menu-link"
      className={cn(
        "flex items-center gap-1.5 rounded-lg p-2 text-xs/relaxed transition-all outline-none hover:bg-muted focus:bg-muted focus-visible:ring-2 focus-visible:ring-ring/30 focus-visible:outline-1 in-data-[slot=navigation-menu-content]:rounded-md data-[active=true]:bg-muted/50 data-[active=true]:hover:bg-muted data-[active=true]:focus:bg-muted [&_svg:not([class*='size-'])]:size-4",
        className
      )}
      {...props}
    />
  )
}

/**
 * base-ui compat — the floating indicator arrow had no RAC equivalent
 * (it tracked the shared viewport's motion). Static markup only.
 */
function NavigationMenuIndicator({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="navigation-menu-indicator"
      data-state="hidden"
      className={cn(
        "top-full z-1 flex h-1.5 items-end justify-center overflow-hidden",
        className
      )}
      {...props}
    >
      <div className="relative top-[60%] h-2 w-2 rotate-45 rounded-tl-sm bg-border shadow-md" />
    </div>
  )
}

export {
  NavigationMenu,
  NavigationMenuContent,
  NavigationMenuIndicator,
  NavigationMenuItem,
  NavigationMenuLink,
  NavigationMenuList,
  NavigationMenuTrigger,
  navigationMenuTriggerStyle,
  NavigationMenuPositioner,
}
