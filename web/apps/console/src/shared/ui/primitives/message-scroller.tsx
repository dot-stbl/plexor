"use client"

import * as React from "react"

import { cn } from "@/lib/utils"
import { Button } from "@/shared/ui/primitives/button"
import { KeyboardDoubleArrowDown } from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * MessageScroller — local implementation (replaces the former
 * third-party message-scroller dependency).
 *
 * Behavior: a scroll container pinned to the end while the user is at
 * the bottom (autoscroll on new content via ResizeObserver), with
 * jump-to-start/end buttons that appear only when the corresponding end
 * is out of view. State lives in a context shared by the Viewport,
 * the buttons, and the exported hooks.
 */

interface MessageScrollerContextValue {
  viewportRef: React.RefObject<HTMLDivElement | null>
  atStart: boolean
  atEnd: boolean
  scrollToStart: (options?: ScrollOptions) => void
  scrollToEnd: (options?: ScrollOptions) => void
  updateEdges: () => void
}

const MessageScrollerContext = React.createContext<MessageScrollerContextValue | null>(null)

function useMessageScrollerContext(hookName: string): MessageScrollerContextValue {
  const context = React.useContext(MessageScrollerContext)
  if (context === null) {
    throw new Error(`${hookName} must be used within a MessageScrollerProvider.`)
  }
  return context
}

const EDGE_THRESHOLD_PX = 4

function MessageScrollerProvider({ children }: { children?: React.ReactNode }) {
  const viewportRef = React.useRef<HTMLDivElement | null>(null)
  const [atStart, setAtStart] = React.useState(true)
  const [atEnd, setAtEnd] = React.useState(true)

  const updateEdges = React.useCallback(() => {
    const viewport = viewportRef.current
    if (viewport === null) {
      return
    }
    const { scrollTop, scrollHeight, clientHeight } = viewport
    setAtStart(scrollTop <= EDGE_THRESHOLD_PX)
    setAtEnd(scrollHeight - scrollTop - clientHeight <= EDGE_THRESHOLD_PX)
  }, [])

  const scrollToStart = React.useCallback((options?: ScrollOptions) => {
    viewportRef.current?.scrollTo({ top: 0, behavior: "smooth", ...options })
  }, [])

  const scrollToEnd = React.useCallback((options?: ScrollOptions) => {
    const viewport = viewportRef.current
    if (viewport === null) {
      return
    }
    viewport.scrollTo({ top: viewport.scrollHeight, behavior: "smooth", ...options })
  }, [])

  // Autoscroll: keep pinned to the end while the user is at the bottom
  // and new content grows the scroll area.
  React.useEffect(() => {
    const viewport = viewportRef.current
    if (viewport === null) {
      return
    }
    const observer = new ResizeObserver(() => {
      if (viewport.scrollHeight - viewport.scrollTop - viewport.clientHeight <= EDGE_THRESHOLD_PX + 16) {
        viewport.scrollTop = viewport.scrollHeight
      }
      updateEdges()
    })
    observer.observe(viewport)
    for (const child of Array.from(viewport.children)) {
      observer.observe(child)
    }
    updateEdges()
    return () => observer.disconnect()
  }, [updateEdges])

  const contextValue = React.useMemo(
    () => ({ viewportRef, atStart, atEnd, scrollToStart, scrollToEnd, updateEdges }),
    [atStart, atEnd, scrollToStart, scrollToEnd, updateEdges],
  )

  return (
    <MessageScrollerContext.Provider value={contextValue}>
      {children}
    </MessageScrollerContext.Provider>
  )
}

function MessageScroller({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="message-scroller"
      className={cn(
        "group/message-scroller relative flex size-full min-h-0 flex-col overflow-hidden",
        className
      )}
      {...props}
    />
  )
}

function MessageScrollerViewport({
  className,
  onScroll,
  ...props
}: React.ComponentProps<"div">) {
  const { viewportRef, updateEdges } = useMessageScrollerContext("MessageScrollerViewport")

  return (
    <div
      ref={viewportRef}
      data-slot="message-scroller-viewport"
      onScroll={(event) => {
        onScroll?.(event)
        updateEdges()
      }}
      className={cn(
        "size-full min-h-0 min-w-0 scroll-fade-b scrollbar-thin scrollbar-gutter-stable overflow-y-auto overscroll-contain contain-content",
        className
      )}
      {...props}
    />
  )
}

function MessageScrollerContent({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="message-scroller-content"
      className={cn("flex h-max min-h-full flex-col gap-6", className)}
      {...props}
    />
  )
}

function MessageScrollerItem({
  className,
  scrollAnchor = false,
  ...props
}: React.ComponentProps<"div"> & { scrollAnchor?: boolean }) {
  return (
    <div
      data-slot="message-scroller-item"
      data-scroll-anchor={scrollAnchor === true ? "" : undefined}
      className={cn(
        "min-w-0 shrink-0 [contain-intrinsic-size:auto_10rem] [content-visibility:auto]",
        className
      )}
      {...props}
    />
  )
}

interface MessageScrollerButtonProps
  extends Omit<React.ComponentProps<typeof Button>, "render" | "variant" | "size"> {
  direction?: "start" | "end"
  variant?: React.ComponentProps<typeof Button>["variant"]
  size?: React.ComponentProps<typeof Button>["size"]
  render?: React.ComponentProps<typeof Button>["render"]
}

function MessageScrollerButton({
  direction = "end",
  className,
  children,
  render,
  variant = "secondary",
  size = "icon-sm",
  ...props
}: MessageScrollerButtonProps) {
  const { atStart, atEnd, scrollToStart, scrollToEnd } =
    useMessageScrollerContext("MessageScrollerButton")
  const active = direction === "end" ? !atEnd : !atStart

  return (
    <Button
      data-slot="message-scroller-button"
      data-direction={direction}
      data-variant={variant}
      data-size={size}
      data-active={active}
      onClick={() => (direction === "end" ? scrollToEnd() : scrollToStart())}
      className={cn(
        "absolute inset-s-1/2 -translate-x-1/2 border-border bg-background text-foreground transition-[translate,scale,opacity] duration-200 hover:bg-muted hover:text-foreground data-[active=false]:pointer-events-none data-[active=false]:scale-95 data-[active=false]:opacity-0 data-[active=false]:duration-400 data-[active=false]:ease-[cubic-bezier(0.7,0,0.84,0)] data-[active=true]:translate-y-0 data-[active=true]:scale-100 data-[active=true]:opacity-100 data-[active=true]:ease-[cubic-bezier(0.23,1,0.32,1)] data-[direction=end]:bottom-4 data-[direction=end]:data-[active=false]:translate-y-full data-[direction=start]:top-4 data-[direction=start]:data-[active=false]:-translate-y-full rtl:translate-x-1/2 data-[direction=start]:[&_svg]:rotate-180",
        className
      )}
      variant={variant}
      size={size}
      render={render}
      {...props}
    >
      {children ?? (
        <>
          <KeyboardDoubleArrowDown strokeWidth={2} />
          <span className="sr-only">
            {direction === "end" ? "Scroll to end" : "Scroll to start"}
          </span>
        </>
      )}
    </Button>
  )
}

/** Scroller API: refs + edge visibility + programmatic scrolling. */
function useMessageScroller() {
  return useMessageScrollerContext("useMessageScroller")
}

/** Props to attach a custom scrollable element to the scroller state. */
function useMessageScrollerScrollable() {
  const { viewportRef, atStart, atEnd } = useMessageScrollerContext("useMessageScrollerScrollable")
  return { ref: viewportRef, atStart, atEnd }
}

/** Edge visibility only. */
function useMessageScrollerVisibility() {
  const { atStart, atEnd } = useMessageScrollerContext("useMessageScrollerVisibility")
  return { atStart, atEnd }
}

export {
  MessageScrollerProvider,
  MessageScroller,
  MessageScrollerViewport,
  MessageScrollerContent,
  MessageScrollerItem,
  MessageScrollerButton,
  useMessageScroller,
  useMessageScrollerScrollable,
  useMessageScrollerVisibility,
}
