"use client"

import * as React from "react"
import {
  Button as RACButton,
  ComboBox as ComboBoxPrimitive,
  ComboBoxStateContext,
  ComboBoxValue as ComboBoxValuePrimitive,
  Header,
  ListBox,
  ListBoxItem,
  Popover,
  Separator as SeparatorPrimitive,
} from "react-aria-components"
import { Check, Close, KeyboardArrowDown } from '@nine-thirty-five/material-symbols-react/rounded/700';

import { cn } from "@/lib/utils"
import { Button } from "@/shared/ui/primitives/button"
import {
  InputGroup,
  InputGroupAddon,
  InputGroupButton,
  InputGroupInput,
} from "@/shared/ui/primitives/input-group"

/**
 * Combobox — react-aria-components-backed (base-ui compat).
 *
 * RAC's ComboBox is single-select; base-ui's chips/multi-select surface
 * (ComboboxChips/Chip/ChipsInput) has no direct RAC equivalent and is
 * kept as styled building blocks wired through props (`onRemove` etc.)
 * rather than internal multi-select state.
 */

interface ComboboxCompatProps
  extends Omit<
    React.ComponentProps<typeof ComboBoxPrimitive>,
    "selectedKey" | "defaultSelectedKey" | "onSelectionChange"
  > {
  /** base-ui compat: selected value. */
  value?: string | null
  /** base-ui compat: selection change handler. */
  onValueChange?: (value: string | null) => void
}

function Combobox({
  value,
  onValueChange,
  ...props
}: ComboboxCompatProps) {
  return (
    <ComboBoxPrimitive
      data-slot="combobox"
      {...(value !== undefined ? { selectedKey: value } : {})}
      onSelectionChange={
        onValueChange !== undefined
          ? (key) => onValueChange(key == null ? null : String(key))
          : undefined
      }
      {...props}
    />
  )
}

function ComboboxValue({
  ...props
}: React.ComponentProps<typeof ComboBoxValuePrimitive>) {
  return <ComboBoxValuePrimitive data-slot="combobox-value" {...props} />
}

function ComboboxTrigger({
  className,
  children,
  ...props
}: React.ComponentProps<typeof RACButton>) {
  return (
    <RACButton
      data-slot="combobox-trigger"
      className={cn("[&_svg:not([class*='size-'])]:size-3.5", className)}
      {...props}
    >
      {children as React.ReactNode}
      <KeyboardArrowDown strokeWidth={2} className="pointer-events-none size-3.5 text-muted-foreground" />
    </RACButton>
  )
}

/** Clears the input + selection via RAC's ComboBox state context. */
function ComboboxClear({ className, ...props }: React.ComponentProps<typeof Button>) {
  const state = React.useContext(ComboBoxStateContext)
  return (
    <Button
      data-slot="combobox-clear"
      variant="ghost"
      size="icon-xs"
      className={cn(className)}
      onClick={() => {
        if (state == null) {
          return
        }
        state.setInputValue("")
        state.setValue(null)
      }}
      {...props}
    >
      <Close strokeWidth={2} className="pointer-events-none" />
    </Button>
  )
}

interface ComboboxInputProps extends Omit<React.ComponentProps<"input">, "children"> {
  disabled?: boolean
  showTrigger?: boolean
  showClear?: boolean
  children?: React.ReactNode
}

function ComboboxInput({
  className,
  children,
  disabled = false,
  showTrigger = true,
  showClear = false,
  ...props
}: ComboboxInputProps) {
  return (
    <InputGroup className={cn("w-auto", className)}>
      {/* RAC Input inside a ComboBox binds to it via context; InputGroupInput
          wraps the house RAC Input with the borderless in-group styling. */}
      <InputGroupInput disabled={disabled} {...props} />
      <InputGroupAddon align="inline-end">
        {showTrigger && (
          <InputGroupButton
            size="icon-xs"
            variant="ghost"
            render={<ComboboxTrigger />}
            data-slot="input-group-button"
            className="group-has-data-[slot=combobox-clear]/input-group:hidden data-pressed:bg-transparent"
            disabled={disabled}
          />
        )}
        {showClear && <ComboboxClear disabled={disabled} />}
      </InputGroupAddon>
      {children}
    </InputGroup>
  )
}

interface ComboboxContentProps
  extends Omit<React.ComponentProps<typeof Popover>, "placement" | "offset"> {
  side?: "top" | "right" | "bottom" | "left"
  align?: "start" | "center" | "end"
  sideOffset?: number
  alignOffset?: number
  /** base-ui compat: anchor element ref (chips layout); unused under RAC. */
  anchor?: React.RefObject<Element | null>
}

function ComboboxContent({
  className,
  side = "bottom",
  sideOffset = 6,
  align = "start",
  alignOffset = 0,
  anchor,
  ...props
}: ComboboxContentProps) {
  void anchor
  return (
    <Popover
      data-slot="combobox-content"
      placement={
        align != null && align !== "center"
          ? side === "top" || side === "bottom"
            ? `${side} ${align}`
            : `${side} ${align === "start" ? "top" : "bottom"}`
          : side
      }
      offset={sideOffset}
      crossOffset={alignOffset}
      className={cn(
        "group/combobox-content relative max-h-(--available-height) w-(--trigger-width) max-w-(--available-width) origin-(--transform-origin) overflow-hidden rounded-lg bg-popover text-popover-foreground shadow-md ring-1 ring-foreground/10 duration-100 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 *:data-[slot=input-group]:m-1 *:data-[slot=input-group]:mb-0 *:data-[slot=input-group]:h-7 *:data-[slot=input-group]:border-none *:data-[slot=input-group]:bg-input/20 *:data-[slot=input-group]:shadow-none dark:bg-popover data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95 data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95",
        className
      )}
      {...props}
    />
  )
}

function ComboboxList({
  className,
  ...props
}: React.ComponentProps<typeof ListBox>) {
  return (
    <ListBox
      data-slot="combobox-list"
      className={cn(
        "no-scrollbar max-h-[min(calc(--spacing(72)---spacing(9)),calc(var(--available-height)---spacing(9)))] scroll-py-1 overflow-y-auto overscroll-contain p-1",
        className
      )}
      {...props}
    />
  )
}

interface ComboboxItemProps
  extends Omit<React.ComponentProps<typeof ListBoxItem>, "children"> {
  children?: React.ReactNode
}

function ComboboxItem({ className, children, ...props }: ComboboxItemProps) {
  return (
    <ListBoxItem
      data-slot="combobox-item"
      className={cn(
        // group/combobox-item drives the selected-check visibility below —
        // children must stay plain (not a render prop) so RAC can extract
        // the item text for filtering.
        "group/combobox-item relative flex min-h-7 w-full cursor-default items-center gap-2 rounded-md px-2 py-1 text-xs/relaxed outline-hidden select-none data-focused:bg-accent data-focused:text-accent-foreground not-data-[variant=destructive]:data-focused:**:text-accent-foreground data-disabled:pointer-events-none data-disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-3.5",
        className
      )}
      {...props}
    >
      {children}
      <span className="pointer-events-none absolute right-2 hidden items-center justify-center group-data-selected/combobox-item:flex">
        <Check strokeWidth={2} />
      </span>
    </ListBoxItem>
  )
}

function ComboboxGroup({
  className,
  ...props
}: React.ComponentProps<"div">) {
  return (
    <div data-slot="combobox-group" className={cn(className)} {...props} />
  )
}

function ComboboxLabel({ className, ...props }: React.ComponentProps<typeof Header>) {
  return (
    <Header
      data-slot="combobox-label"
      className={cn("px-2 py-1.5 text-xs text-muted-foreground", className)}
      {...props}
    />
  )
}

/** base-ui compat passthrough — grouping is expressed via sections/items directly. */
function ComboboxCollection({ children, ...props }: React.ComponentProps<"div">) {
  return (
    <div data-slot="combobox-collection" {...props}>
      {children}
    </div>
  )
}

/** Renders when the list has no items (driven by RAC's ComboBox state). */
function ComboboxEmpty({ className, ...props }: React.ComponentProps<"div">) {
  const state = React.useContext(ComboBoxStateContext)
  if (state != null && state.collection.size > 0) {
    return null
  }
  return (
    <div
      data-slot="combobox-empty"
      className={cn(
        "flex w-full justify-center py-2 text-center text-xs/relaxed text-muted-foreground",
        className
      )}
      {...props}
    />
  )
}

function ComboboxSeparator({
  className,
  ...props
}: React.ComponentProps<typeof SeparatorPrimitive>) {
  return (
    <SeparatorPrimitive
      data-slot="combobox-separator"
      className={cn("-mx-1 my-1 h-px bg-border/50", className)}
      {...props}
    />
  )
}

function ComboboxChips({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="combobox-chips"
      className={cn(
        "flex min-h-7 flex-wrap items-center gap-1 rounded-md border border-input bg-input/20 bg-clip-padding px-2 py-0.5 text-xs/relaxed transition-colors focus-within:border-ring focus-within:ring-2 focus-within:ring-ring/30 has-aria-invalid:border-destructive has-aria-invalid:ring-2 has-aria-invalid:ring-destructive/20 has-data-[slot=combobox-chip]:px-1 dark:bg-input/30 dark:has-aria-invalid:border-destructive/50 dark:has-aria-invalid:ring-destructive/40",
        className
      )}
      {...props}
    />
  )
}

interface ComboboxChipProps extends React.ComponentProps<"span"> {
  showRemove?: boolean
  /** Called when the chip's remove button is pressed. */
  onRemove?: () => void
}

function ComboboxChip({
  className,
  children,
  showRemove = true,
  onRemove,
  ...props
}: ComboboxChipProps) {
  return (
    <span
      data-slot="combobox-chip"
      className={cn(
        "flex h-[calc(--spacing(4.75))] w-fit items-center justify-center gap-1 rounded-[calc(var(--radius-sm)-2px)] bg-muted-foreground/10 px-1.5 text-xs/relaxed font-medium whitespace-nowrap text-foreground has-disabled:pointer-events-none has-disabled:cursor-not-allowed has-disabled:opacity-50 has-data-[slot=combobox-chip-remove]:pr-0",
        className
      )}
      {...props}
    >
      {children}
      {showRemove && (
        <Button
          variant="ghost"
          size="icon-xs"
          onClick={onRemove}
          className="-ml-1 opacity-50 hover:opacity-100"
          data-slot="combobox-chip-remove"
        >
          <Close strokeWidth={2} className="pointer-events-none" />
        </Button>
      )}
    </span>
  )
}

function ComboboxChipsInput({
  className,
  ...props
}: React.ComponentProps<"input">) {
  return (
    <input
      data-slot="combobox-chip-input"
      className={cn("min-w-16 flex-1 outline-none", className)}
      {...props}
    />
  )
}

function useComboboxAnchor() {
  return React.useRef<HTMLDivElement | null>(null)
}

export {
  Combobox,
  ComboboxInput,
  ComboboxContent,
  ComboboxList,
  ComboboxItem,
  ComboboxGroup,
  ComboboxLabel,
  ComboboxCollection,
  ComboboxEmpty,
  ComboboxSeparator,
  ComboboxChips,
  ComboboxChip,
  ComboboxChipsInput,
  ComboboxTrigger,
  ComboboxValue,
  useComboboxAnchor,
}
