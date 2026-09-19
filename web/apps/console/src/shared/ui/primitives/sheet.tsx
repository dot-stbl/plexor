import * as React from "react"
import {
  DialogTrigger,
  Heading,
  Modal,
  ModalOverlay,
} from "react-aria-components"
import { Close } from "@nine-thirty-five/material-symbols-react/rounded/700"

import { cn } from "@/lib/utils"
import { Button } from "@/shared/ui/primitives/button"

/**
 * Plexor Sheet — react-aria-components-backed.
 *
 * Mirrors the base-ui-era API: `<Sheet open={open} onOpenChange={...}>` with
 * `<SheetContent side={...}>` inside. Backed by ModalOverlay + Modal.
 */
type SheetRootProps = {
  /** base-ui compat: open state. */
  open?: boolean
  /** base-ui compat: open-state setter. */
  onOpenChange?: (open: boolean) => void
  dismissible?: boolean
  children?: React.ReactNode
}

function Sheet({ open = true, onOpenChange, dismissible = true, children, ...props }: SheetRootProps) {
  return (
    <ModalOverlay
      data-slot="sheet"
      isDismissable={dismissible}
      isOpen={open}
      onOpenChange={onOpenChange}
      {...props}
    >
      {children}
    </ModalOverlay>
  )
}

function SheetClose(props: React.ComponentProps<typeof Button>) {
  return <Button data-slot="sheet-close" slot="close" {...props} />
}

interface SheetContentProps extends React.ComponentProps<typeof Modal> {
  side?: "top" | "right" | "bottom" | "left"
  showCloseButton?: boolean
  children?: React.ReactNode
}

function SheetContent({
  className,
  children,
  side = "right",
  showCloseButton = true,
  ...props
}: SheetContentProps) {
  return (
    <Modal
      data-slot="sheet-content"
      data-side={side}
      className={cn(
        "fixed z-50 flex flex-col bg-popover bg-clip-padding text-xs/relaxed text-popover-foreground shadow-lg transition duration-200 ease-in-out data-[side=bottom]:inset-x-0 data-[side=bottom]:bottom-0 data-[side=bottom]:h-auto data-[side=bottom]:border-t data-[side=bottom]:data-entering:translate-y-[2.5rem] data-[side=bottom]:data-exiting:translate-y-[2.5rem] data-[side=left]:inset-y-0 data-[side=left]:left-0 data-[side=left]:h-full data-[side=left]:w-3/4 data-[side=left]:border-r data-[side=left]:data-entering:translate-x-[-2.5rem] data-[side=left]:data-exiting:translate-x-[-2.5rem] data-[side=right]:inset-y-0 data-[side=right]:right-0 data-[side=right]:h-full data-[side=right]:w-3/4 data-[side=right]:border-l data-[side=right]:data-entering:translate-x-[2.5rem] data-[side=right]:data-exiting:translate-x-[2.5rem] data-[side=top]:inset-x-0 data-[side=top]:top-0 data-[side=top]:h-auto data-[side=top]:border-b data-[side=top]:data-entering:translate-y-[-2.5rem] data-[side=top]:data-exiting:translate-y-[-2.5rem] data-[side=left]:sm:max-w-sm data-[side=right]:sm:max-w-sm",
        className
      )}
      {...props}
    >
      {children}
      {showCloseButton && (
        <Button
          slot="close"
          variant="ghost"
          className="absolute top-4 right-4"
          size="icon-sm"
        >
          <Close strokeWidth={2} />
          <span className="sr-only">Close</span>
        </Button>
      )}
    </Modal>
  )
}

function SheetOverlay({ className, ...props }: React.ComponentProps<typeof ModalOverlay>) {
  return (
    <ModalOverlay
      data-slot="sheet-overlay"
      className={cn(
        "fixed inset-0 isolate z-50 bg-black/80 transition-opacity duration-150 data-entering:opacity-0 data-exiting:opacity-0 supports-backdrop-filter:backdrop-blur-md",
        className
      )}
      {...props}
    />
  )
}

function SheetHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="sheet-header"
      className={cn("flex flex-col gap-1.5 p-6", className)}
      {...props}
    />
  )
}

function SheetFooter({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="sheet-footer"
      className={cn("mt-auto flex flex-col gap-2 p-6", className)}
      {...props}
    />
  )
}

function SheetTitle({ className, ...props }: React.ComponentProps<typeof Heading>) {
  return (
    <Heading
      data-slot="sheet-title"
      className={cn("font-heading text-sm font-medium text-foreground", className)}
      {...props}
    />
  )
}

function SheetDescription({ className, ...props }: React.ComponentProps<"p">) {
  return (
    <p
      data-slot="sheet-description"
      className={cn("text-xs/relaxed text-muted-foreground", className)}
      {...props}
    />
  )
}

const SheetTrigger = DialogTrigger

export {
  Sheet,
  SheetTrigger,
  SheetClose,
  SheetContent,
  SheetOverlay,
  SheetHeader,
  SheetFooter,
  SheetTitle,
  SheetDescription,
}
