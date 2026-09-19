"use client"

import * as React from "react"
import {
  Dialog,
  DialogTrigger,
  Heading,
  Modal,
  ModalOverlay,
  Text,
} from "react-aria-components"

import { cn } from "@/lib/utils"
import { Button } from "@/shared/ui/primitives/button"

/**
 * AlertDialog — react-aria-components-backed (base-ui compat).
 *
 * Structure: ModalOverlay (open/onOpenChange compat) > Modal (positioning)
 * > Dialog role="alertdialog" (semantics + focus). Alert dialogs are not
 * light-dismissable by default (`dismissible={false}`), matching the
 * destructive-confirm intent; cancel/action buttons close via
 * `slot="close"`.
 */
type AlertDialogRootProps = {
  /** base-ui compat: open state. */
  open?: boolean
  /** base-ui compat: open-state setter. */
  onOpenChange?: (open: boolean) => void
  /** Whether clicking the backdrop / pressing Escape dismisses. Defaults false (alert semantics). */
  dismissible?: boolean
  children?: React.ReactNode
}

function AlertDialog({
  open = false,
  onOpenChange,
  dismissible = false,
  children,
  ...props
}: AlertDialogRootProps) {
  return (
    <ModalOverlay
      data-slot="alert-dialog"
      isDismissable={dismissible}
      isKeyboardDismissDisabled={!dismissible}
      isOpen={open}
      onOpenChange={onOpenChange}
      {...props}
    >
      {children}
    </ModalOverlay>
  )
}

/**
 * base-ui compat passthrough — RAC portals overlays automatically.
 * Accepts children (rendered) so existing call sites keep working.
 */
function AlertDialogPortal({ children, ...props }: React.ComponentProps<"div">) {
  return (
    <div data-slot="alert-dialog-portal" {...props}>
      {children}
    </div>
  )
}

/**
 * base-ui compat passthrough — the backdrop tint. Under RAC the overlay
 * IS the ModalOverlay element, so this renders a sibling backdrop div
 * when used standalone; AlertDialogContent already composes it.
 */
function AlertDialogOverlay({
  className,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="alert-dialog-overlay"
      aria-hidden="true"
      className={cn(
        "fixed inset-0 isolate z-50 bg-black/80 duration-100 supports-backdrop-filter:backdrop-blur-md",
        className
      )}
      {...props}
    />
  )
}

interface AlertDialogContentProps
  extends Omit<React.ComponentProps<typeof Modal>, "className" | "children"> {
  size?: "default" | "sm"
  className?: string
  children?: React.ReactNode
}

function AlertDialogContent({
  className,
  size = "default",
  children,
  ...props
}: AlertDialogContentProps) {
  return (
    <Modal
      data-slot="alert-dialog-content"
      data-size={size}
      className={cn(
        "fixed top-1/2 left-1/2 z-50 -translate-x-1/2 -translate-y-1/2",
        className
      )}
      {...props}
    >
      <Dialog
        role="alertdialog"
        data-size={size}
        className={cn(
          "group/alert-dialog-content grid w-full gap-3 rounded-xl bg-popover p-4 text-popover-foreground ring-1 ring-foreground/10 duration-100 outline-none data-[size=default]:max-w-xs data-[size=sm]:max-w-64 data-[size=default]:sm:max-w-sm",
          "data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95 data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95"
        )}
      >
        {children}
      </Dialog>
    </Modal>
  )
}

function AlertDialogHeader({
  className,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="alert-dialog-header"
      className={cn(
        "grid grid-rows-[auto_1fr] place-items-center gap-1 text-center has-data-[slot=alert-dialog-media]:grid-rows-[auto_auto_1fr] has-data-[slot=alert-dialog-media]:gap-x-4 sm:group-data-[size=default]/alert-dialog-content:place-items-start sm:group-data-[size=default]/alert-dialog-content:text-left sm:group-data-[size=default]/alert-dialog-content:has-data-[slot=alert-dialog-media]:grid-rows-[auto_1fr]",
        className
      )}
      {...props}
    />
  )
}

function AlertDialogFooter({
  className,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="alert-dialog-footer"
      className={cn(
        "flex flex-col-reverse gap-2 group-data-[size=sm]/alert-dialog-content:grid group-data-[size=sm]/alert-dialog-content:grid-cols-2 sm:flex-row sm:justify-end",
        className
      )}
      {...props}
    />
  )
}

function AlertDialogMedia({
  className,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="alert-dialog-media"
      className={cn(
        "mb-2 inline-flex size-8 items-center justify-center rounded-md bg-muted sm:group-data-[size=default]/alert-dialog-content:row-span-2 *:[svg:not([class*='size-'])]:size-4",
        className
      )}
      {...props}
    />
  )
}

function AlertDialogTitle({
  className,
  ...props
}: React.ComponentProps<typeof Heading>) {
  return (
    <Heading
      data-slot="alert-dialog-title"
      className={cn(
        "font-heading text-sm font-medium sm:group-data-[size=default]/alert-dialog-content:group-has-data-[slot=alert-dialog-media]/alert-dialog-content:col-start-2",
        className
      )}
      {...props}
    />
  )
}

function AlertDialogDescription({
  className,
  ...props
}: React.ComponentProps<typeof Text>) {
  return (
    <Text
      slot="description"
      data-slot="alert-dialog-description"
      className={cn(
        "text-xs/relaxed text-balance text-muted-foreground md:text-pretty *:[a]:underline *:[a]:underline-offset-3 *:[a]:hover:text-foreground",
        className
      )}
      {...props}
    />
  )
}

function AlertDialogAction({
  className,
  ...props
}: React.ComponentProps<typeof Button>) {
  return (
    <Button
      data-slot="alert-dialog-action"
      slot="close"
      className={cn(className)}
      {...props}
    />
  )
}

function AlertDialogCancel({
  className,
  variant = "outline",
  size = "default",
  ...props
}: React.ComponentProps<typeof Button> &
  Pick<React.ComponentProps<typeof Button>, "variant" | "size">) {
  return (
    <Button
      data-slot="alert-dialog-cancel"
      slot="close"
      variant={variant}
      size={size}
      className={cn(className)}
      {...props}
    />
  )
}

const AlertDialogTrigger = DialogTrigger

export {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogMedia,
  AlertDialogOverlay,
  AlertDialogPortal,
  AlertDialogTitle,
  AlertDialogTrigger,
}
