"use client"

import * as React from "react"
import {
  DialogTrigger,
  Heading,
  Modal,
  ModalOverlay,
  Text,
} from "react-aria-components"

import { cn } from "@/lib/utils"
import { Button } from "@/shared/ui/primitives/button"

/**
 * Drawer — react-aria-components-backed (base-ui compat).
 *
 * RAC has no drawer primitive: this composes ModalOverlay + Modal with
 * slide-in transitions (the Sheet pattern). Base UI's swipe gestures
 * (swipeDirection, snapPoints, swipe-to-dismiss) have no RAC equivalent —
 * the props stay in the API for compatibility but only drive the
 * slide-in side and the static swipe-handle affordance.
 */
type DrawerSide = "top" | "right" | "bottom" | "left"

interface DrawerRootProps {
  /** Controlled open state. */
  open?: boolean
  /** Controlled open-state setter. */
  onOpenChange?: (open: boolean) => void
  /** Whether the drawer blocks interaction with the rest of the page. */
  modal?: boolean
  /** base-ui compat: swipe direction — maps to the drawer's edge. */
  swipeDirection?: "up" | "down" | "left" | "right"
  /** base-ui compat: snap points (not supported under RAC; inert). */
  snapPoints?: number[] | null
  /** base-ui compat: show the drag handle affordance. */
  showSwipeHandle?: boolean
  children?: React.ReactNode
}

const DrawerContext = React.createContext<{
  showSwipeHandle: boolean
  swipeDirection: NonNullable<DrawerRootProps["swipeDirection"]>
} | null>(null)

function useDrawer() {
  const context = React.useContext(DrawerContext)
  if (!context) {
    throw new Error("useDrawer must be used within a Drawer.")
  }
  return context
}

function sideFromDirection(
  direction: NonNullable<DrawerRootProps["swipeDirection"]>,
): DrawerSide {
  return direction === "up" ? "top" : direction === "down" ? "bottom" : direction
}

function Drawer({
  open,
  onOpenChange,
  modal = true,
  showSwipeHandle = false,
  snapPoints: _snapPoints,
  swipeDirection = "down",
  children,
  ...props
}: DrawerRootProps) {
  const contextValue = React.useMemo(
    () => ({ showSwipeHandle, swipeDirection }),
    [showSwipeHandle, swipeDirection],
  )

  return (
    <DrawerContext.Provider value={contextValue}>
      <ModalOverlay
        data-slot="drawer"
        data-modal={modal}
        isOpen={open}
        onOpenChange={onOpenChange}
        isDismissable={modal}
        {...props}
      >
        {children}
      </ModalOverlay>
    </DrawerContext.Provider>
  )
}

const DrawerTrigger = DialogTrigger

/** base-ui compat passthrough — RAC portals overlays automatically. */
function DrawerPortal({ children }: { children?: React.ReactNode }) {
  return <>{children}</>
}

/** Backdrop tint rendered inside the ModalOverlay underlay. */
function DrawerOverlay({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="drawer-overlay"
      aria-hidden="true"
      className={cn(
        "fixed inset-0 z-50 bg-black/80 duration-450 ease-[cubic-bezier(0.32,0.72,0,1)] select-none supports-backdrop-filter:backdrop-blur-md data-entering:opacity-0 data-exiting:opacity-0",
        className
      )}
      {...props}
    />
  )
}

function DrawerClose(props: React.ComponentProps<typeof Button>) {
  return <Button data-slot="drawer-close" slot="close" {...props} />
}

function DrawerSwipeHandle({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="drawer-swipe-handle"
      aria-hidden="true"
      className={cn(
        "relative z-10 flex shrink-0 cursor-grab transition-opacity duration-200 items-center justify-center h-3 w-full after:block after:shrink-0 after:h-1 after:w-12 after:rounded-full after:bg-muted active:cursor-grabbing",
        className
      )}
      {...props}
    />
  )
}

interface DrawerContentProps extends Omit<React.ComponentProps<typeof Modal>, "children"> {
  children?: React.ReactNode
  side?: DrawerSide
}

function DrawerContent({ className, children, ...props }: DrawerContentProps) {
  const { showSwipeHandle, swipeDirection } = useDrawer()
  const side = sideFromDirection(swipeDirection)

  return (
    <>
      <DrawerOverlay />
      <Modal
        data-slot="drawer-popup"
        data-swipe-direction={swipeDirection}
        className={cn(
          "group/drawer-popup fixed z-50 m-2 flex flex-col rounded-xl border border-popover bg-popover text-xs/relaxed text-popover-foreground shadow-lg duration-450 ease-[cubic-bezier(0.22,1,0.36,1)] outline-none select-none dark:border-border",
          "data-entering:animate-in data-entering:fade-in-0 data-exiting:animate-out data-exiting:fade-out-0",
          // Slide-in per side (replaces base-ui's transform-driven swipe).
          "data-[side=bottom]:inset-x-0 data-[side=bottom]:bottom-0 data-[side=bottom]:max-h-[90dvh] data-[side=bottom]:rounded-b-none data-[side=bottom]:data-entering:slide-in-from-bottom data-[side=bottom]:data-entering:duration-300",
          "data-[side=top]:inset-x-0 data-[side=top]:top-0 data-[side=top]:max-h-[90dvh] data-[side=top]:rounded-t-none data-[side=top]:data-entering:slide-in-from-top data-[side=top]:data-entering:duration-300",
          "data-[side=left]:inset-y-0 data-[side=left]:left-0 data-[side=left]:w-3/4 data-[side=left]:sm:w-96 data-[side=left]:rounded-l-none data-[side=left]:data-entering:slide-in-from-left data-[side=left]:data-entering:duration-300",
          "data-[side=right]:inset-y-0 data-[side=right]:right-0 data-[side=right]:w-3/4 data-[side=right]:sm:w-96 data-[side=right]:rounded-r-none data-[side=right]:data-entering:slide-in-from-right data-[side=right]:data-entering:duration-300",
          className
        )}
        data-side={side}
        {...props}
      >
        {showSwipeHandle && <DrawerSwipeHandle />}
        <div
          data-slot="drawer-content"
          className="flex min-h-0 flex-1 flex-col overflow-hidden overscroll-contain rounded-[inherit] select-text"
        >
          {children}
        </div>
      </Modal>
    </>
  )
}

function DrawerHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="drawer-header"
      className={cn("flex shrink-0 flex-col gap-1 p-4 pb-0", className)}
      {...props}
    />
  )
}

function DrawerFooter({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="drawer-footer"
      className={cn("mt-auto flex shrink-0 flex-col gap-2 p-4 pt-0", className)}
      {...props}
    />
  )
}

function DrawerTitle({ className, ...props }: React.ComponentProps<typeof Heading>) {
  return (
    <Heading
      data-slot="drawer-title"
      className={cn("font-heading text-sm font-medium text-foreground", className)}
      {...props}
    />
  )
}

function DrawerDescription({ className, ...props }: React.ComponentProps<typeof Text>) {
  return (
    <Text
      slot="description"
      data-slot="drawer-description"
      className={cn("text-xs/relaxed text-balance text-muted-foreground", className)}
      {...props}
    />
  )
}

export {
  Drawer,
  DrawerPortal,
  DrawerOverlay,
  DrawerSwipeHandle,
  DrawerTrigger,
  DrawerClose,
  DrawerContent,
  DrawerHeader,
  DrawerFooter,
  DrawerTitle,
  DrawerDescription,
}
