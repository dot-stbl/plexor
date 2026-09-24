"use client"

import * as React from "react"
import { Check } from "@nine-thirty-five/material-symbols-react/rounded/700"
import { MenuItem as RAMenuItem, MenuSection } from "react-aria-components"

import { cn } from "@/lib/utils"

interface DropdownMenuItemProps extends Omit<React.ComponentProps<typeof RAMenuItem>, "onAction" | "onClick" | "children"> {
  children?: React.ReactNode
  className?: string
  inset?: boolean
  variant?: "default" | "destructive"
  onClick?: React.MouseEventHandler<HTMLElement>
  isDisabled?: boolean
  disabled?: boolean
}

export function DropdownMenuItem({
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

export function DropdownMenuGroup({ children }: { children?: React.ReactNode }) {
  return <MenuSection data-slot="dropdown-menu-group">{children}</MenuSection>
}
DropdownMenuGroup.displayName = "PlexorDropdownMenuGroup"

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
export const DropdownMenuLabel = Object.assign(DropdownMenuLabelInner, { displayName: "PlexorDropdownMenuLabel" })

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
export const DropdownMenuSeparator = Object.assign(DropdownMenuSeparatorInner, { displayName: "PlexorDropdownMenuSeparator" })

interface DropdownMenuCheckboxItemProps {
  children?: React.ReactNode
  className?: string
  inset?: boolean
  checked?: boolean
  onCheckedChange?: (checked: boolean) => void
  disabled?: boolean
}

export function DropdownMenuCheckboxItem({ className, inset, children, checked, onCheckedChange, disabled }: DropdownMenuCheckboxItemProps) {
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

export function DropdownMenuRadioGroup({ children }: { children?: React.ReactNode }) {
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

export function DropdownMenuRadioItem({ className, inset, children, selected, onSelect, disabled }: DropdownMenuRadioItemProps) {
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

export function DropdownMenuShortcut({ className, ...props }: React.HTMLAttributes<HTMLSpanElement>) {
  return (
    <span
      data-slot="dropdown-menu-shortcut"
      className={cn("ml-auto text-[0.625rem] tracking-widest text-muted-foreground group-focus/dropdown-menu-item:text-accent-foreground", className)}
      {...props}
    />
  )
}
