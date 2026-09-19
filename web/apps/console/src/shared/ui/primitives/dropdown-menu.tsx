"use client"

import * as React from "react"
import { Check, KeyboardArrowRight } from "@nine-thirty-five/material-symbols-react/rounded/700"
import {
  Menu as RACMenu,
  MenuItem as RAMenuItem,
  MenuTrigger,
  MenuSection,
  Popover,
  Pressable,
} from "react-aria-components"

import { cn } from "@/lib/utils"

/**
 * DropdownMenu — Plexor DS wrapper around react-aria-components' Menu.
 *
 * Compatibility shims vs base-ui:
 *   - `<DropdownMenuTrigger render={<Button>}>`: clones the render target
 *     with our trigger className merged in.
 *   - `<DropdownMenuItem>` accepts `onClick` (HTML) → RAC `onAction`
 *   - `<DropdownMenuItem inset variant="destructive">` map to data attrs
 *     via the wrapper's className
 *   - Submenu: kept as a thin wrapper over `<Menu>` (RAC requires a
 *     `<SubmenuTrigger>` inside a parent `<Menu>`; we make this work
 *     structurally)
 */

interface DropdownMenuTriggerProps {
  render?: React.ReactNode
  className?: string
  children?: React.ReactNode
  isDisabled?: boolean
  disabled?: boolean
  onPress?: () => void
}

function DropdownMenuTrigger({ render, className, children, isDisabled, disabled, onPress }: DropdownMenuTriggerProps) {
  const target = (render ?? children) as React.ReactElement | undefined
  if (!React.isValidElement(target)) {
    return (
      <Pressable isDisabled={isDisabled ?? disabled}>
        <button
          type="button"
          className={className}
          disabled={disabled ?? isDisabled}
          onClick={onPress}
          data-slot="dropdown-menu-trigger"
        >
          {children}
        </button>
      </Pressable>
    )
  }
  // Wrap the trigger element in a Pressable so MenuTrigger sees a pressable
  // child (RAC requires this; bare <button> doesn't wire open/close).
  // Pressable's children prop is typed as Partial<unknown> & DOMAttributes,
  // which clashes with arbitrary render targets (Link, A, etc.). We cast
  // at the Pressable boundary to keep the public API flexible.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const PressableAny = Pressable as unknown as React.FC<{ isDisabled?: boolean; children?: any }>
  const targetProps = target.props as { className?: string }
  const mergedClassName = cn(targetProps.className, className)
  // Only forward `children` into the cloned target when the caller passed a
  // `render` element. Without `render`, `target` IS `children` (a single
  // element the caller wants rendered as-is), and re-passing it would
  // self-nest the element. With `render`, the render element is the target
  // and the caller's siblings are the children we need to thread through;
  // Button.composeRender's `children ?? original.children` then receives them.
  const clonedProps =
    render !== undefined ? { className: mergedClassName, children } : { className: mergedClassName }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const cloned = React.cloneElement(target, clonedProps as any)
  return (
    <PressableAny isDisabled={isDisabled ?? disabled}>{cloned}</PressableAny>
  )
}
DropdownMenuTrigger.displayName = "PlexorDropdownMenuTrigger"

interface DropdownMenuContentProps extends React.HTMLAttributes<HTMLDivElement> {
  align?: "start" | "center" | "end"
  alignOffset?: number
  side?: "top" | "right" | "bottom" | "left"
  sideOffset?: number
  children?: React.ReactNode
  className?: string
}

/**
 * Render an inner DropdownMenuContent's children:
 * - DropdownMenuItem / DropdownMenuCheckboxItem / DropdownMenuRadioItem → wrapped in RACMenuItem
 * - DropdownMenuSeparator → plain divider inside Menu
 * - DropdownMenuLabel / DropdownMenuGroup → plain header/section
 *
 * RAC's Menu only honours MenuItem descendants for keyboard nav, but it
 * also renders other children literally between menu items, so we can
 * place Separator/Label as siblings of MenuItem without breaking layout.
 */
function DropdownMenuContent({ className, children, side = "bottom", sideOffset = 4, align = "start" }: DropdownMenuContentProps) {
  // Render Separator/Label **outside** RACMenu (as siblings in the popover),
  // and items **inside** RACMenu. RAC's Menu strips unknown children even
  // when they're real primitives, so mixing them at the Menu level would
  // hide them.
  const arr = flattenChildren(children)
  const menuItems: React.ReactNode[] = []
  const meta: React.ReactNode[] = []
  for (let i = 0; i < arr.length; i++) {
    const c = arr[i]!
    const display = (c.type as { displayName?: string })?.displayName
    if (display === "PlexorDropdownMenuSeparator" || display === "PlexorDropdownMenuLabel") {
      meta.push(c)
    } else {
      menuItems.push(c)
    }
  }
  return (
    <Popover
      data-slot="dropdown-menu-content"
      placement={side}
      offset={sideOffset}
      {...(align ? { align } : {})}
      className={cn(
        "z-50 max-h-(--available-height) w-(--anchor-width) min-w-32 origin-(--transform-origin) overflow-x-hidden overflow-y-auto rounded-lg bg-popover p-1 text-popover-foreground shadow-md ring-1 ring-foreground/10 duration-100 outline-none",
        "data-[side=bottom]:slide-in-from-top-2 data-[side=inline-end]:slide-in-from-left-2 data-[side=inline-start]:slide-in-from-right-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
        "data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95",
        "data-exiting:animate-out data-exiting:overflow-hidden data-exiting:fade-out-0 data-exiting:zoom-out-95",
        className
      )}
    >
      {meta}
      <RACMenu>{menuItems}</RACMenu>
    </Popover>
  )
}

interface DropdownMenuItemProps extends Omit<React.ComponentProps<typeof RAMenuItem>, "onAction" | "onClick" | "children"> {
  children?: React.ReactNode
  className?: string
  inset?: boolean
  variant?: "default" | "destructive"
  onClick?: React.MouseEventHandler<HTMLElement>
  isDisabled?: boolean
  disabled?: boolean
}

function DropdownMenuItem({
  className,
  inset,
  variant = "default",
  onClick,
  isDisabled,
  disabled,
  children,
  ...props
}: DropdownMenuItemProps) {
  // RAC: HTML onClick handler is replaced with onAction (no event).
  // We accept onClick (HTML) and translate: extract the handler and call it
  // via onAction.
  const handleAction = React.useCallback(() => {
    if (onClick) {
      // SyntheticEvent args not available; emulate a no-op event for compat.
      // Callers usually only care about the click semantics.
      onClick({} as unknown as React.MouseEvent<HTMLElement>)
    }
  }, [onClick])
  return (
    <RAMenuItem
      data-slot="dropdown-menu-item"
      data-inset={inset}
      data-variant={variant}
      isDisabled={isDisabled ?? disabled}
      onAction={onClick ? handleAction : undefined}
      className={cn(
        "group/dropdown-menu-item relative flex min-h-7 cursor-default items-center gap-2 rounded-md px-2 py-1 text-xs/relaxed outline-hidden select-none",
        "data-[focused=true]:bg-accent data-[focused=true]:text-accent-foreground",
        "data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
        "[&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-3.5",
        "data-inset:pl-7.5",
        variant === "destructive" && "data-[variant=destructive]:text-destructive data-[variant=destructive]:data-[focused=true]:bg-destructive/10 data-[variant=destructive]:data-[focused=true]:text-destructive dark:data-[variant=destructive]:data-[focused=true]:bg-destructive/20 data-[variant=destructive]:*:[svg]:text-destructive",
        className
      )}
      {...props}
    >
      {children}
    </RAMenuItem>
  )
}

function DropdownMenuGroup({ children }: { children?: React.ReactNode }) {
  return <MenuSection data-slot="dropdown-menu-group">{children}</MenuSection>
}
DropdownMenuGroup.displayName = "PlexorDropdownMenuGroup"

interface DropdownMenuLabelProps {
  className?: string
  inset?: boolean
  children?: React.ReactNode
}

interface DropdownMenuLabelProps extends React.HTMLAttributes<HTMLDivElement> {
  inset?: boolean
}

function DropdownMenuLabelInner({ className, inset, children, ...props }: DropdownMenuLabelProps) {
  return (
    <div
      data-slot="dropdown-menu-label"
      data-inset={inset}
      className={cn("px-2 py-1.5 text-xs text-muted-foreground", inset && "pl-7.5", className)}
      {...props}
    >
      {children}
    </div>
  )
}
const DropdownMenuLabel = Object.assign(DropdownMenuLabelInner, { displayName: "PlexorDropdownMenuLabel" })

type DropdownMenuSeparatorProps = React.HTMLAttributes<HTMLDivElement>

function DropdownMenuSeparatorInner({ className, ...props }: DropdownMenuSeparatorProps) {
  return (
    <div
      data-slot="dropdown-menu-separator"
      className={cn("-mx-1 my-1 h-px bg-border/50", className)}
      {...props}
    />
  )
}
const DropdownMenuSeparator = Object.assign(DropdownMenuSeparatorInner, { displayName: "PlexorDropdownMenuSeparator" })

interface DropdownMenuCheckboxItemProps {
  children?: React.ReactNode
  className?: string
  inset?: boolean
  checked?: boolean
  onCheckedChange?: (checked: boolean) => void
  disabled?: boolean
}

function DropdownMenuCheckboxItem({ className, inset, children, checked, onCheckedChange, disabled }: DropdownMenuCheckboxItemProps) {
  return (
    <RAMenuItem
      data-slot="dropdown-menu-checkbox-item"
      data-inset={inset}
      isDisabled={disabled}
      onAction={() => onCheckedChange?.(!checked)}
      className={cn(
        "relative flex min-h-7 cursor-default items-center gap-2 rounded-md py-1.5 pr-8 pl-2 text-xs outline-hidden select-none",
        "data-[focused=true]:bg-accent data-[focused=true]:text-accent-foreground",
        "data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
        "[&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-3.5",
        "data-inset:pl-7.5",
        className
      )}
    >
      {children}
      <span
        data-slot="dropdown-menu-checkbox-item-indicator"
        className="pointer-events-none absolute right-2 flex items-center justify-center"
      >
        {checked ? <Check strokeWidth={2} /> : null}
      </span>
    </RAMenuItem>
  )
}

function DropdownMenuRadioGroup({ children }: { children?: React.ReactNode }) {
  return <MenuSection data-slot="dropdown-menu-radio-group">{children}</MenuSection>
}

interface DropdownMenuRadioItemProps {
  children?: React.ReactNode
  className?: string
  inset?: boolean
  selected?: boolean
  onSelect?: () => void
  disabled?: boolean
}

function DropdownMenuRadioItem({ className, inset, children, selected, onSelect, disabled }: DropdownMenuRadioItemProps) {
  return (
    <RAMenuItem
      data-slot="dropdown-menu-radio-item"
      data-inset={inset}
      isDisabled={disabled}
      onAction={onSelect}
      className={cn(
        "relative flex min-h-7 cursor-default items-center gap-2 rounded-md py-1.5 pr-8 pl-2 text-xs outline-hidden select-none",
        "data-[focused=true]:bg-accent data-[focused=true]:text-accent-foreground",
        "data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
        "[&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-3.5",
        "data-inset:pl-7.5",
        className
      )}
    >
      {children}
      <span
        data-slot="dropdown-menu-radio-item-indicator"
        className="pointer-events-none absolute right-2 flex items-center justify-center"
      >
        {selected ? <Check strokeWidth={2} /> : null}
      </span>
    </RAMenuItem>
  )
}

function DropdownMenuShortcut({ className, ...props }: React.HTMLAttributes<HTMLSpanElement>) {
  return (
    <span
      data-slot="dropdown-menu-shortcut"
      className={cn("ml-auto text-[0.625rem] tracking-widest text-muted-foreground group-focus/dropdown-menu-item:text-accent-foreground", className)}
      {...props}
    />
  )
}

/**
 * Submenu — RAC's `<SubmenuTrigger>` must be inside a parent `<Menu>`.
 * For the legacy base-ui-style API where submenus are declarative siblings,
 * we render a Trigger with KeyboardArrowRight arrow next to children.
 * Limitations: nesting beyond one level is not supported in this version.
 */
interface DropdownMenuSubTriggerProps extends React.HTMLAttributes<HTMLDivElement> {
  className?: string
  inset?: boolean
  children?: React.ReactNode
}

function DropdownMenuSubTrigger({ className, inset, children, ...props }: DropdownMenuSubTriggerProps) {
  return (
    <div
      data-slot="dropdown-menu-sub-trigger"
      data-inset={inset}
      className={cn(
        "flex min-h-7 cursor-default items-center gap-2 rounded-md px-2 py-1 text-xs outline-hidden select-none",
        "data-popup-open:bg-accent data-popup-open:text-accent-foreground data-open:bg-accent data-open:text-accent-foreground",
        "[&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-3.5",
        "data-inset:pl-7.5",
        className
      )}
      {...props}
    >
      {children}
      <KeyboardArrowRight strokeWidth={2} className="ml-auto" />
    </div>
  )
}

function DropdownMenuSubContent({ className, children }: DropdownMenuContentProps) {
  return (
    <Popover
      data-slot="dropdown-menu-sub-content"
      className={cn(
        "z-50 w-auto min-w-32 rounded-lg bg-popover p-1 text-popover-foreground shadow-md ring-1 ring-foreground/10 duration-100",
        "data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
        "data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95",
        "data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95",
        className
      )}
    >
      {children}
    </Popover>
  )
}

function DropdownMenuSub({ children }: { children?: React.ReactNode }) {
  void children
  return null
}

/**
 * Wrapper: <DropdownMenu>{trigger, content, ...}</DropdownMenu>. RAC's
 * MenuTrigger expects both the trigger element and a `<Menu>` component as
 * siblings. We split the children and wrap with MenuTrigger. Fragments
 * and DropdownMenuGroup wrappers are flattened so callers can wrap
 * siblings in `<>...</>` or `<DropdownMenuGroup>...</DropdownMenuGroup>`.
 */
function flattenChildren(children: React.ReactNode): React.ReactElement[] {
  const out: React.ReactElement[] = []
  React.Children.forEach(children, (c) => {
    if (!React.isValidElement(c)) return
    const display = (c.type as { displayName?: string })?.displayName
    // Recurse into transparent wrappers so Labels inside Groups surface to
    // the slot dispatcher in DropdownMenuContent. Without this, the Label's
    // slot name (`PlexorDropdownMenuLabel`) is hidden behind the Group and
    // never matched against the registry.
    if (display === "PlexorDropdownMenuGroup" || c.type === React.Fragment) {
      const groupChildren = (c.props as { children?: React.ReactNode }).children
      out.push(...flattenChildren(groupChildren))
      return
    }
    out.push(c)
  })
  return out
}

function DropdownMenu({ children }: { children?: React.ReactNode }) {
  const arr = flattenChildren(children)
  const trigger = arr.find(
    (c) => (c.type as { displayName?: string })?.displayName === "PlexorDropdownMenuTrigger"
  )
  const others = arr.filter(
    (c) => (c.type as { displayName?: string })?.displayName !== "PlexorDropdownMenuTrigger"
  )
  if (!trigger) {
    return <>{children}</>
  }
  return (
    <MenuTrigger>
      {trigger}
      {others}
    </MenuTrigger>
  )
}

/** Compatibility: base-ui DropdownMenuPortal — RAC doesn't have portals in the same way. */
function DropdownMenuPortal({ children }: { children?: React.ReactNode }) {
  return <>{children}</>
}

export {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuItem,
  DropdownMenuCheckboxItem,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuSub,
  DropdownMenuSubTrigger,
  DropdownMenuSubContent,
  DropdownMenuTrigger,
  DropdownMenuPortal,
}
