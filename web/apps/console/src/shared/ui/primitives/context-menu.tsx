"use client"

import * as React from "react"
import { Check, KeyboardArrowRight } from '@nine-thirty-five/material-symbols-react/rounded/700';
import {
  Menu as RACMenu,
  MenuItem as RAMenuItem,
  MenuSection,
  MenuTrigger,
  Popover,
  Pressable,
} from "react-aria-components"

import { cn } from "@/lib/utils"

/**
 * ContextMenu — react-aria-components-backed (base-ui compat).
 *
 * RAC's `<MenuTrigger trigger="contextMenu">` handles the right-click:
 * the menu opens at the pointer (offset 0, bottom-start) with full
 * keyboard/ARIA menu semantics. Structure mirrors DropdownMenu:
 * the ContextMenu root splits its children into the trigger and the
 * content, items become RAC MenuItem (onAction), labels/separators
 * render inside the popover but outside the Menu.
 */

interface ContextMenuRootProps {
  /** Controlled open state. */
  open?: boolean
  /** Controlled open-state setter. */
  onOpenChange?: (open: boolean) => void
  children?: React.ReactNode
}

function flattenChildren(children: React.ReactNode): React.ReactElement[] {
  const out: React.ReactElement[] = []
  React.Children.forEach(children, (child) => {
    if (!React.isValidElement(child)) {
      return
    }
    const display = (child.type as { displayName?: string })?.displayName
    if (display === "PlexorContextMenuGroup" || child.type === React.Fragment) {
      const groupChildren = (child.props as { children?: React.ReactNode }).children
      out.push(...flattenChildren(groupChildren))
      return
    }
    out.push(child)
  })
  return out
}

function ContextMenu({ open, onOpenChange, children }: ContextMenuRootProps) {
  const arr = flattenChildren(children)
  const trigger = arr.find(
    (child) => (child.type as { displayName?: string })?.displayName === "PlexorContextMenuTrigger",
  )
  const others = arr.filter(
    (child) => (child.type as { displayName?: string })?.displayName !== "PlexorContextMenuTrigger",
  )
  if (!trigger) {
    return <>{children}</>
  }
  return (
    <MenuTrigger trigger="contextMenu" isOpen={open} onOpenChange={onOpenChange}>
      {trigger}
      {others}
    </MenuTrigger>
  )
}

interface ContextMenuTriggerProps {
  children?: React.ReactNode
  className?: string
}

/**
 * MenuTrigger delivers its context-menu handlers via React context
 * (PressResponder) — only RAC pressables consume them. Wrap the target
 * in `<Pressable>` (same approach as DropdownMenuTrigger) so any child
 * element becomes the context-menu target.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const PressableAny = Pressable as unknown as React.FC<{ children?: any }>

function ContextMenuTrigger({ children, className }: ContextMenuTriggerProps) {
  const arr = React.Children.toArray(children)
  if (arr.length === 1 && React.isValidElement(arr[0])) {
    const target = arr[0]
    const original = target.props as { className?: string }
    const cloned = React.cloneElement(target, {
      className: cn(original.className, "select-none", className),
    } as Record<string, unknown>)
    return <PressableAny>{cloned}</PressableAny>
  }
  return (
    <PressableAny>
      <span data-slot="context-menu-trigger" className={cn("select-none", className)}>
        {children}
      </span>
    </PressableAny>
  )
}
ContextMenuTrigger.displayName = "PlexorContextMenuTrigger"

/** base-ui compat: RAC portals overlays automatically. */
function ContextMenuPortal({ children }: { children?: React.ReactNode }) {
  return <>{children}</>
}

interface ContextMenuContentProps extends React.HTMLAttributes<HTMLDivElement> {
  align?: "start" | "center" | "end"
  alignOffset?: number
  side?: "top" | "right" | "bottom" | "left"
  sideOffset?: number
  children?: React.ReactNode
  className?: string
}

function ContextMenuContent({
  className,
  children,
  side,
  sideOffset = 0,
  align: _align,
}: ContextMenuContentProps) {
  // Without an explicit `side`, RAC anchors the context menu at the
  // pointer (bottom-start, offset 0) — the native context-menu feel.
  // `align` is accepted for API compatibility but has no RAC equivalent
  // on a context menu.
  const arr = flattenChildren(children)
  const menuItems: React.ReactNode[] = []
  const meta: React.ReactNode[] = []
  for (const child of arr) {
    const display = (child.type as { displayName?: string })?.displayName
    if (display === "PlexorContextMenuSeparator" || display === "PlexorContextMenuLabel") {
      meta.push(child)
    } else {
      menuItems.push(child)
    }
  }
  return (
    <Popover
      data-slot="context-menu-content"
      {...(side != null ? { placement: side } : {})}
      offset={sideOffset}
      className={cn(
        "z-50 max-h-(--available-height) min-w-32 origin-(--transform-origin) overflow-x-hidden overflow-y-auto rounded-lg bg-popover p-1 text-popover-foreground shadow-md ring-1 ring-foreground/10 duration-100 outline-none",
        "data-[side=bottom]:slide-in-from-top-2 data-[side=inline-end]:slide-in-from-left-2 data-[side=inline-start]:slide-in-from-right-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
        "data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95 data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95",
        className
      )}
    >
      {meta}
      <RACMenu>{menuItems}</RACMenu>
    </Popover>
  )
}

function ContextMenuGroup({ children }: { children?: React.ReactNode }) {
  return <MenuSection data-slot="context-menu-group">{children}</MenuSection>
}
ContextMenuGroup.displayName = "PlexorContextMenuGroup"

interface ContextMenuLabelProps extends React.HTMLAttributes<HTMLDivElement> {
  inset?: boolean
}

function ContextMenuLabel({ className, inset, ...props }: ContextMenuLabelProps) {
  return (
    <div
      data-slot="context-menu-label"
      data-inset={inset}
      className={cn(
        "px-2 py-1.5 text-xs text-muted-foreground data-inset:pl-7.5",
        className
      )}
      {...props}
    />
  )
}
ContextMenuLabel.displayName = "PlexorContextMenuLabel"

interface ContextMenuItemProps {
  children?: React.ReactNode
  className?: string
  inset?: boolean
  variant?: "default" | "destructive"
  disabled?: boolean
  onClick?: React.MouseEventHandler<HTMLElement>
}

function ContextMenuItem({
  className,
  inset,
  variant = "default",
  disabled,
  onClick,
  children,
}: ContextMenuItemProps) {
  return (
    <RAMenuItem
      data-slot="context-menu-item"
      data-inset={inset}
      data-variant={variant}
      isDisabled={disabled}
      onAction={onClick ? () => onClick({} as unknown as React.MouseEvent<HTMLElement>) : undefined}
      className={cn(
        "group/context-menu-item relative flex min-h-7 cursor-default items-center gap-2 rounded-md px-2 py-1 text-xs/relaxed outline-hidden select-none",
        "data-[focused=true]:bg-accent data-[focused=true]:text-accent-foreground",
        "not-data-[variant=destructive]:data-[focused=true]:**:text-accent-foreground",
        "data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
        "data-inset:pl-7.5",
        "data-[variant=destructive]:text-destructive data-[variant=destructive]:data-[focused=true]:bg-destructive/10 data-[variant=destructive]:data-[focused=true]:text-destructive dark:data-[variant=destructive]:data-[focused=true]:bg-destructive/20 data-[variant=destructive]:*:[svg]:text-destructive",
        "[&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-3.5",
        className
      )}
    >
      {children}
    </RAMenuItem>
  )
}

/** base-ui compat: declarative submenu wrappers are no-ops (see DropdownMenuSub). */
function ContextMenuSub({ children }: { children?: React.ReactNode }) {
  void children
  return null
}

interface ContextMenuSubTriggerProps extends React.HTMLAttributes<HTMLDivElement> {
  inset?: boolean
}

function ContextMenuSubTrigger({
  className,
  inset,
  children,
  ...props
}: ContextMenuSubTriggerProps) {
  return (
    <div
      data-slot="context-menu-sub-trigger"
      data-inset={inset}
      className={cn(
        "flex min-h-7 cursor-default items-center gap-2 rounded-md px-2 py-1 text-xs outline-hidden select-none data-[focused=true]:bg-accent data-[focused=true]:text-accent-foreground not-data-[variant=destructive]:data-[focused=true]:**:text-accent-foreground data-inset:pl-7.5 data-popup-open:bg-accent data-popup-open:text-accent-foreground [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-3.5",
        className
      )}
      {...props}
    >
      {children}
      <KeyboardArrowRight strokeWidth={2} className="ml-auto" />
    </div>
  )
}

function ContextMenuSubContent({
  className,
  children,
  ...props
}: ContextMenuContentProps) {
  return (
    <ContextMenuContent className={cn("shadow-lg", className)} {...props}>
      {children}
    </ContextMenuContent>
  )
}

interface ContextMenuCheckboxItemProps {
  children?: React.ReactNode
  className?: string
  inset?: boolean
  checked?: boolean
  onCheckedChange?: (checked: boolean) => void
  disabled?: boolean
}

function ContextMenuCheckboxItem({
  className,
  inset,
  children,
  checked,
  onCheckedChange,
  disabled,
}: ContextMenuCheckboxItemProps) {
  return (
    <RAMenuItem
      data-slot="context-menu-checkbox-item"
      data-inset={inset}
      isDisabled={disabled}
      onAction={() => onCheckedChange?.(!checked)}
      className={cn(
        "relative flex min-h-7 cursor-default items-center gap-2 rounded-md py-1.5 pr-8 pl-2 text-xs outline-hidden select-none",
        "data-[focused=true]:bg-accent data-[focused=true]:text-accent-foreground data-[focused=true]:**:text-accent-foreground",
        "data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
        "data-inset:pl-7.5",
        "[&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-3.5",
        className
      )}
    >
      {children}
      <span
        data-slot="context-menu-checkbox-item-indicator"
        className="pointer-events-none absolute right-2 flex items-center justify-center"
      >
        {checked ? <Check strokeWidth={2} /> : null}
      </span>
    </RAMenuItem>
  )
}

interface ContextMenuRadioGroupProps {
  children?: React.ReactNode
}

function ContextMenuRadioGroup({ children }: ContextMenuRadioGroupProps) {
  return <MenuSection data-slot="context-menu-radio-group">{children}</MenuSection>
}

interface ContextMenuRadioItemProps {
  children?: React.ReactNode
  className?: string
  inset?: boolean
  selected?: boolean
  onSelect?: () => void
  disabled?: boolean
}

function ContextMenuRadioItem({
  className,
  inset,
  children,
  selected,
  onSelect,
  disabled,
}: ContextMenuRadioItemProps) {
  return (
    <RAMenuItem
      data-slot="context-menu-radio-item"
      data-inset={inset}
      isDisabled={disabled}
      onAction={onSelect}
      className={cn(
        "relative flex min-h-7 cursor-default items-center gap-2 rounded-md py-1.5 pr-8 pl-2 text-xs outline-hidden select-none",
        "data-[focused=true]:bg-accent data-[focused=true]:text-accent-foreground data-[focused=true]:**:text-accent-foreground",
        "data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
        "data-inset:pl-7.5",
        "[&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-3.5",
        className
      )}
    >
      {children}
      <span
        data-slot="context-menu-radio-item-indicator"
        className="pointer-events-none absolute right-2 flex items-center justify-center"
      >
        {selected ? <Check strokeWidth={2} /> : null}
      </span>
    </RAMenuItem>
  )
}

function ContextMenuSeparator({
  className,
  ...props
}: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      data-slot="context-menu-separator"
      className={cn("-mx-1 my-1 h-px bg-border/50", className)}
      {...props}
    />
  )
}
ContextMenuSeparator.displayName = "PlexorContextMenuSeparator"

function ContextMenuShortcut({
  className,
  ...props
}: React.HTMLAttributes<HTMLSpanElement>) {
  return (
    <span
      data-slot="context-menu-shortcut"
      className={cn(
        "ml-auto text-[0.625rem] tracking-widest text-muted-foreground group-data-[focused=true]/context-menu-item:text-accent-foreground",
        className
      )}
      {...props}
    />
  )
}

export {
  ContextMenu,
  ContextMenuTrigger,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuCheckboxItem,
  ContextMenuRadioItem,
  ContextMenuLabel,
  ContextMenuSeparator,
  ContextMenuShortcut,
  ContextMenuGroup,
  ContextMenuPortal,
  ContextMenuSub,
  ContextMenuSubContent,
  ContextMenuSubTrigger,
  ContextMenuRadioGroup,
}
