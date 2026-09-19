"use client"

import * as React from "react"
import { Popover as PopoverPrimitive, type Placement } from "react-aria-components"

import { cn } from "@/lib/utils"

/**
 * base-ui side/align → RAC Placement (left/right express cross alignment
 * as top/bottom).
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
 * HoverCard — react-aria-components has no dedicated hover-card primitive,
 * so this composes one from RAC's `Popover` in controlled mode
 * (`triggerRef` + `isOpen`) plus plain pointer/focus events:
 *
 *   - trigger: hover (after `openDelay`) or focus opens;
 *   - leaving the trigger starts a `closeDelay` timer (cancelled by
 *     entering the card — the classic hover bridge);
 *   - the card itself is a non-modal popover that never self-closes on
 *     outside interaction; open state is owned entirely here.
 */

interface HoverCardContextValue {
  isOpen: boolean
  triggerRef: React.RefObject<Element | null>
  openWithDelay: () => void
  closeWithDelay: () => void
  cancelClose: () => void
}

const HoverCardContext = React.createContext<HoverCardContextValue | null>(null)

function useHoverCardContext(componentName: string): HoverCardContextValue {
  const context = React.useContext(HoverCardContext)
  if (context === null) {
    throw new Error(`${componentName} must be used within a HoverCard.`)
  }
  return context
}

interface HoverCardRootProps {
  /** Controlled open state. */
  open?: boolean
  /** Controlled open-state setter. */
  onOpenChange?: (open: boolean) => void
  /** Hover-to-open delay in ms. */
  openDelay?: number
  /** Hover-to-close delay in ms (leave time to cross onto the card). */
  closeDelay?: number
  children?: React.ReactNode
}

function HoverCard({
  open: openProp,
  onOpenChange,
  openDelay = 300,
  closeDelay = 100,
  children,
}: HoverCardRootProps) {
  const [uncontrolledOpen, setUncontrolledOpen] = React.useState(false)
  const isOpen = openProp ?? uncontrolledOpen
  const triggerRef = React.useRef<Element | null>(null)
  const openTimer = React.useRef<number | null>(null)
  const closeTimer = React.useRef<number | null>(null)

  const setOpen = React.useCallback(
    (next: boolean) => {
      if (onOpenChange !== undefined) {
        onOpenChange(next)
      } else {
        setUncontrolledOpen(next)
      }
    },
    [onOpenChange],
  )

  const clearTimers = React.useCallback(() => {
    if (openTimer.current !== null) {
      window.clearTimeout(openTimer.current)
      openTimer.current = null
    }
    if (closeTimer.current !== null) {
      window.clearTimeout(closeTimer.current)
      closeTimer.current = null
    }
  }, [])

  const openWithDelay = React.useCallback(() => {
    if (closeTimer.current !== null) {
      window.clearTimeout(closeTimer.current)
      closeTimer.current = null
    }
    if (openTimer.current !== null) {
      return
    }
    openTimer.current = window.setTimeout(() => {
      openTimer.current = null
      setOpen(true)
    }, openDelay)
  }, [openDelay, setOpen])

  const closeWithDelay = React.useCallback(() => {
    if (openTimer.current !== null) {
      window.clearTimeout(openTimer.current)
      openTimer.current = null
    }
    if (closeTimer.current !== null) {
      return
    }
    closeTimer.current = window.setTimeout(() => {
      closeTimer.current = null
      setOpen(false)
    }, closeDelay)
  }, [closeDelay, setOpen])

  const cancelClose = React.useCallback(() => {
    if (closeTimer.current !== null) {
      window.clearTimeout(closeTimer.current)
      closeTimer.current = null
    }
  }, [])

  React.useEffect(() => clearTimers, [clearTimers])

  const contextValue = React.useMemo(
    () => ({ isOpen, triggerRef, openWithDelay, closeWithDelay, cancelClose }),
    [isOpen, openWithDelay, closeWithDelay, cancelClose],
  )

  return (
    <HoverCardContext.Provider value={contextValue}>
      {children}
    </HoverCardContext.Provider>
  )
}

type Renderable = React.ReactElement<Record<string, unknown>>

interface HoverCardTriggerProps {
  /** Render the trigger as the given element (props merged, ours appended). */
  render?: Renderable
  children?: React.ReactNode
  className?: string
}

function chain(original: unknown, ours: (event: unknown) => void) {
  if (typeof original !== "function") {
    return ours
  }
  return (event: unknown) => {
    (original as (event: unknown) => void)(event)
    ours(event)
  }
}

function HoverCardTrigger({ render, children, className }: HoverCardTriggerProps) {
  const { triggerRef, openWithDelay, closeWithDelay } = useHoverCardContext("HoverCardTrigger")

  const setTriggerRef = React.useCallback(
    (element: Element | null) => {
      triggerRef.current = element
    },
    [triggerRef],
  )

  const target = render

  if (target !== undefined && React.isValidElement(target)) {
    const original = target.props as {
      className?: string
      ref?: React.Ref<unknown>
      onPointerEnter?: (event: React.PointerEvent) => void
      onPointerLeave?: (event: React.PointerEvent) => void
      onFocus?: (event: React.FocusEvent) => void
      onBlur?: (event: React.FocusEvent) => void
    }
    return React.cloneElement(target, {
      ...original,
      className: cn(original.className, className),
      ref: chain(original.ref, setTriggerRef as unknown as (value: unknown) => void),
      onPointerEnter: chain(original.onPointerEnter, openWithDelay),
      onPointerLeave: chain(original.onPointerLeave, closeWithDelay),
      onFocus: chain(original.onFocus, openWithDelay),
      onBlur: chain(original.onBlur, closeWithDelay),
    } as Record<string, unknown>)
  }

  return (
    <span
      data-slot="hover-card-trigger"
      ref={setTriggerRef}
      className={className}
      tabIndex={0}
      onPointerEnter={openWithDelay}
      onPointerLeave={closeWithDelay}
      onFocus={openWithDelay}
      onBlur={closeWithDelay}
    >
      {children}
    </span>
  )
}

interface HoverCardContentProps {
  side?: "top" | "right" | "bottom" | "left"
  sideOffset?: number
  align?: "start" | "center" | "end"
  alignOffset?: number
  className?: string
  children?: React.ReactNode
}

function HoverCardContent({
  className,
  align,
  alignOffset = 0,
  side = "bottom",
  sideOffset = 4,
  children,
  ...props
}: HoverCardContentProps) {
  const { isOpen, triggerRef, closeWithDelay, cancelClose } =
    useHoverCardContext("HoverCardContent")

  return (
    <PopoverPrimitive
      data-slot="hover-card-content"
      triggerRef={triggerRef}
      isOpen={isOpen}
      isNonModal
      shouldCloseOnInteractOutside={() => false}
      placement={toPlacement(side, align)}
      offset={sideOffset}
      crossOffset={alignOffset}
      className={cn(
        "z-50 w-72 origin-(--transform-origin) rounded-lg bg-popover p-2.5 text-xs/relaxed text-popover-foreground shadow-md ring-1 ring-foreground/10 outline-hidden duration-100 data-[side=bottom]:slide-in-from-top-2 data-[side=inline-end]:slide-in-from-left-2 data-[side=inline-start]:slide-in-from-right-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95 data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95",
        className
      )}
      {...props}
    >
      <div onPointerEnter={cancelClose} onPointerLeave={closeWithDelay}>
        {children}
      </div>
    </PopoverPrimitive>
  )
}

export { HoverCard, HoverCardTrigger, HoverCardContent }
