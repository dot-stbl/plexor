import * as React from "react"
import {
  DialogTrigger,
  Heading,
  Popover as PopoverPrimitive,
  Text,
  type Placement,
} from "react-aria-components"

import { cn } from "@/lib/utils"
import { Button } from "@/shared/ui/primitives/button"

/**
 * base-ui side/align → RAC Placement. top/bottom take start/end on the
 * cross axis; left/right express it as top/bottom instead.
 */
function toPlacement(
  side: "top" | "right" | "bottom" | "left",
  align?: "start" | "center" | "end",
): Placement {
  if (align == null || align === "center") {
    return side
  }
  if (side === "top" || side === "bottom") {
    return `${side} ${align}`
  }
  return `${side} ${align === "start" ? "top" : "bottom"}`
}

/**
 * Popover — react-aria-components-backed (base-ui compat).
 *
 * `<Popover open onOpenChange>` maps to RAC's `<DialogTrigger isOpen
 * onOpenChange>`; the trigger is the first child (`PopoverTrigger`) and
 * the overlay the second (`PopoverContent` → RAC `Popover`).
 */
interface PopoverRootProps {
  /** base-ui compat: open state. */
  open?: boolean
  /** base-ui compat: open-state setter. */
  onOpenChange?: (open: boolean) => void
  /** base-ui compat: open delay (kept for API stability; RAC opens on press). */
  openDelay?: number
  children?: React.ReactNode
}

function Popover({ open, onOpenChange, children, ...props }: PopoverRootProps) {
  return (
    <DialogTrigger
      data-slot="popover"
      isOpen={open}
      onOpenChange={onOpenChange}
      {...props}
    >
      {children}
    </DialogTrigger>
  )
}

type Renderable = React.ReactElement<{
  className?: string
  children?: React.ReactNode
}>

interface PopoverTriggerProps {
  /** base-ui compat: render the trigger as the given element. */
  render?: Renderable
  /** Fallback content when no `render` element is provided. */
  children?: React.ReactNode
  className?: string
}

/**
 * base-ui compat trigger. DialogTrigger clones this component with the
 * press/focus/aria props it needs; we forward them onto the render
 * target (caller children win over the render element's own — Button's
 * composeRender semantics).
 */
function PopoverTrigger({ render, className, children, ...props }: PopoverTriggerProps) {
  if (render !== undefined && React.isValidElement(render)) {
    const original = render.props
    return React.cloneElement(render, {
      ...props,
      className: cn(original.className, className),
      children: children ?? original.children,
    } as typeof original & Record<string, unknown>)
  }
  return (
    <Button data-slot="popover-trigger" className={className} {...props}>
      {children}
    </Button>
  )
}

interface PopoverContentProps
  extends Omit<
    React.ComponentProps<typeof PopoverPrimitive>,
    "placement" | "offset" | "crossOffset"
  > {
  /** base-ui compat: edge ("top" | "right" | "bottom" | "left"). */
  side?: "top" | "right" | "bottom" | "left"
  /** base-ui compat: distance from the trigger in px. */
  sideOffset?: number
  /** base-ui compat: cross-axis alignment. */
  align?: "start" | "center" | "end"
  /** base-ui compat: cross-axis offset in px. */
  alignOffset?: number
  className?: string
  children?: React.ReactNode
}

function PopoverContent({
  className,
  align,
  alignOffset = 0,
  side = "bottom",
  sideOffset = 4,
  ...props
}: PopoverContentProps) {
  return (
    <PopoverPrimitive
      data-slot="popover-content"
      placement={toPlacement(side, align)}
      offset={sideOffset}
      crossOffset={alignOffset}
      className={cn(
        "z-50 flex w-72 origin-(--transform-origin) flex-col gap-4 rounded-lg bg-popover p-2.5 text-xs text-popover-foreground shadow-md ring-1 ring-foreground/10 outline-hidden duration-100 data-[side=bottom]:slide-in-from-top-2 data-[side=inline-end]:slide-in-from-left-2 data-[side=inline-start]:slide-in-from-right-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95 data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95",
        className
      )}
      {...props}
    />
  )
}

function PopoverHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="popover-header"
      className={cn("flex flex-col gap-1 text-xs", className)}
      {...props}
    />
  )
}

function PopoverTitle({ className, ...props }: React.ComponentProps<typeof Heading>) {
  return (
    <Heading
      data-slot="popover-title"
      className={cn("text-sm font-medium", className)}
      {...props}
    />
  )
}

function PopoverDescription({ className, ...props }: React.ComponentProps<typeof Text>) {
  return (
    <Text
      slot="description"
      data-slot="popover-description"
      className={cn("text-muted-foreground", className)}
      {...props}
    />
  )
}

export {
  Popover,
  PopoverContent,
  PopoverDescription,
  PopoverHeader,
  PopoverTitle,
  PopoverTrigger,
}
