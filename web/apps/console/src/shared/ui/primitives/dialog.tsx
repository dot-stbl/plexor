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
 * Plexor Dialog — react-aria-components-backed.
 *
 * Mirrors the base-ui-era `<Dialog open={open} onOpenChange={...}>` API:
 * the `<Dialog>` element manages open state and renders a ModalOverlay
 * that contains its children (typically DialogContent).
 *
 * For a dialog with a separate trigger button, use `<DialogTrigger>`
 * (re-exported) which wires its child's onPress to DialogContent open state.
 */
type DialogRootProps = {
  /** base-ui compat: open state. */
  open?: boolean
  /** base-ui compat: open-state setter. */
  onOpenChange?: (open: boolean) => void
  /** base-ui compat: clicking the backdrop dismisses. */
  dismissible?: boolean
  children?: React.ReactNode
}

function Dialog({ open = true, onOpenChange, dismissible = true, children, ...props }: DialogRootProps) {
  return (
    <ModalOverlay
      data-slot="dialog"
      isDismissable={dismissible}
      isOpen={open}
      onOpenChange={onOpenChange}
      {...props}
    >
      {children}
    </ModalOverlay>
  )
}

function DialogClose(props: React.ComponentProps<typeof Button>) {
  return <Button data-slot="dialog-close" slot="close" {...props} />
}

function DialogOverlay({ className, ...props }: React.ComponentProps<typeof ModalOverlay>) {
  return (
    <ModalOverlay
      data-slot="dialog-overlay"
      className={cn(
        "fixed inset-0 isolate z-modal-backdrop bg-black/80 duration-100 supports-backdrop-filter:backdrop-blur-md data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95 data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95",
        className
      )}
      {...props}
    />
  )
}

interface DialogContentProps extends React.ComponentProps<typeof Modal> {
  showCloseButton?: boolean
  children?: React.ReactNode
}

function DialogContent({ className, children, showCloseButton = true, ...props }: DialogContentProps) {
  return (
    <Modal
      data-slot="dialog-content"
      className={cn(
        "fixed top-1/2 left-1/2 z-modal grid w-full max-w-[calc(100%-2rem)] -translate-x-1/2 -translate-y-1/2 gap-4 rounded-xl bg-popover p-4 text-xs/relaxed text-popover-foreground ring-1 ring-foreground/10 duration-100 outline-none sm:max-w-sm data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95 data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95",
        className
      )}
      {...props}
    >
      {children}
      {showCloseButton && (
        <Button
          slot="close"
          variant="ghost"
          className="absolute top-2 right-2"
          size="icon-sm"
        >
          <Close strokeWidth={2} />
          <span className="sr-only">Close</span>
        </Button>
      )}
    </Modal>
  )
}

function DialogHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="dialog-header"
      className={cn("flex flex-col gap-1", className)}
      {...props}
    />
  )
}

interface DialogFooterProps extends React.ComponentProps<"div"> {
  showCloseButton?: boolean
}

function DialogFooter({ className, showCloseButton = false, children, ...props }: DialogFooterProps) {
  return (
    <div
      data-slot="dialog-footer"
      className={cn(
        "flex flex-col-reverse gap-2 sm:flex-row sm:justify-end",
        className
      )}
      {...props}
    >
      {children}
      {showCloseButton && (
        <Button slot="close" variant="outline">
          Close
        </Button>
      )}
    </div>
  )
}

function DialogTitle({ className, ...props }: React.ComponentProps<typeof Heading>) {
  return (
    <Heading
      data-slot="dialog-title"
      className={cn("font-heading text-sm font-medium", className)}
      {...props}
    />
  )
}

function DialogDescription({ className, ...props }: React.ComponentProps<"p">) {
  return (
    <p
      data-slot="dialog-description"
      className={cn(
        "text-xs/relaxed text-muted-foreground *:[a]:underline *:[a]:underline-offset-3 *:[a]:hover:text-foreground",
        className
      )}
      {...props}
    />
  )
}

export {
  Dialog,
  DialogTrigger,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogOverlay,
  DialogTitle,
}
