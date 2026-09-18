"use client"

import * as React from "react"
import { Tooltip as TooltipPrimitive, TooltipTrigger as RACTooltipTrigger } from "react-aria-components"

import { cn } from "@/lib/utils"

interface TooltipContextValue {
  delay: number
  closeDelay: number
}

const TooltipContext = React.createContext<TooltipContextValue>({ delay: 0, closeDelay: 0 })

interface TooltipProviderProps {
  /** Open delay in ms. Defaults to 0 (open instantly on hover/focus). */
  delay?: number
  /** Close delay in ms. Defaults to 0. */
  closeDelay?: number
  children?: React.ReactNode
}

function TooltipProvider({ delay = 0, closeDelay = 0, children }: TooltipProviderProps) {
  return (
    <TooltipContext.Provider value={{ delay, closeDelay }}>
      {children}
    </TooltipContext.Provider>
  )
}

interface TooltipRootProps {
  /** base-ui compat: open state (uncontrolled). */
  open?: boolean
  /** base-ui compat: open-state setter. */
  onOpenChange?: (open: boolean) => void
  /** Open delay in ms. */
  delay?: number
  /** Close delay in ms. */
  closeDelay?: number
  /** base-ui compat: render as the given element. */
  children?: React.ReactNode
}

/**
 * Plexor Tooltip root — compat shim.
 *
 * The previous (base-ui) API put the trigger and popup as siblings inside a
 * `<Tooltip>` root. react-aria-components' equivalent is `<TooltipTrigger>`
 * wrapping both. We bridge by extracting the first `<TooltipTrigger>` and
 * `<TooltipContent>` from `children`, then delegating to RAC's
 * `<TooltipTrigger>` with the right children.
 */
function TooltipRoot({ children, open, ...props }: TooltipRootProps) {
  // Pattern A: children are [<TooltipTrigger>, <TooltipContent>] siblings.
  // Pass them through to RAC's <TooltipTrigger>.
  const arr = React.Children.toArray(children)
  if (arr.length === 2) {
    const trigger = arr[0]!
    const content = arr[1]!
    const ctx = React.useContext(TooltipContext)
    return (
      <RACTooltipTrigger
        delay={props.delay ?? ctx.delay}
        closeDelay={props.closeDelay ?? ctx.closeDelay}
        isOpen={open}
      >
        {trigger}
        {content}
      </RACTooltipTrigger>
    )
  }

  // Pattern B: a single child — pass through. Useful when used as a passthrough.
  return (
    <TooltipPrimitive
      data-slot="tooltip"
      isOpen={open}
      {...(props as Omit<React.ComponentProps<typeof TooltipPrimitive>, "isOpen">)}
    >
      {children}
    </TooltipPrimitive>
  )
}

const Tooltip = TooltipRoot

type Renderable =
  | React.ReactNode
  | ((props: Record<string, unknown>, ...rest: unknown[]) => React.ReactNode)
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  | ((props: any, ...rest: any[]) => React.ReactNode)

interface TooltipTriggerProps {
  /** base-ui compat: render the trigger as the given element or render-fn. */
  render?: Renderable
  /** base-ui compat: delay. */
  delay?: number
  /** base-ui compat: closeDelay. */
  closeDelay?: number
  /** Used when not using `render`. */
  children?: React.ReactNode
  className?: string
}

/**
 * base-ui compat — `<TooltipTrigger render={<Button>}>` used to render the
 * trigger element with our props merged in. In RAC, the trigger element is
 * the first child of `<TooltipTrigger>`. We clone the render target with our
 * props so callers don't need to change. Supports React elements and
 * `(props) => ReactNode` render functions.
 */
function TooltipTrigger({ render, className, children }: TooltipTriggerProps) {
  const target = render ?? children
  if (typeof target === "function") {
    return <>{target({ className } as Record<string, unknown>)}</>
  }
  if (!React.isValidElement(target)) {
    return <>{target}</>
  }
  const targetProps = (target.props ?? {}) as { className?: string; children?: React.ReactNode }
  const merged = {
    ...targetProps,
    className: cn(targetProps.className, className),
  }
  return React.cloneElement(target, merged)
}

interface TooltipContentProps extends Omit<React.ComponentProps<typeof TooltipPrimitive>, "placement" | "offset" | "children"> {
  /** base-ui compat: alias for placement. */
  side?: "top" | "right" | "bottom" | "left"
  /** base-ui compat: alias for offset. */
  sideOffset?: number
  /** align axis. */
  align?: "start" | "center" | "end"
  /** Visible when false; rendered but with display:none via hidden attr. */
  hidden?: boolean
  children?: React.ReactNode
  className?: string
}

function TooltipContent({
  className,
  children,
  side = "top",
  sideOffset = 4,
  align,
  ...props
}: TooltipContentProps) {
  return (
    <TooltipPrimitive
      data-slot="tooltip-content"
      placement={side}
      offset={sideOffset}
      {...(align ? { align } : {})}
      className={cn(
        "z-50 inline-flex w-fit max-w-xs origin-(--transform-origin) items-center gap-1.5 rounded-md bg-foreground px-3 py-1.5 text-xs text-background has-data-[slot=kbd]:pr-1.5 data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95 data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95",
        className
      )}
      {...props}
    >
      {children}
    </TooltipPrimitive>
  )
}

export { Tooltip, TooltipTrigger, TooltipContent, TooltipProvider }
