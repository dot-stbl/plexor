"use client"

import * as React from "react"
import {
  Button as PrimitiveButton,
  ListBox,
  ListBoxItem,
  Popover,
  Select as RACSelect,
  SelectValue as RACSelectValue,
} from "react-aria-components"
import { Check, KeyboardArrowDown } from "@nine-thirty-five/material-symbols-react/rounded/700"

import { cn } from "@/lib/utils"

/**
 * Select — Plexor DS wrapper around react-aria-components' Select.
 *
 * Compatibility shims vs base-ui:
 *   - `value` ↔ RAC `selectedKey`
 *   - `onValueChange` ↔ RAC `onSelectionChange`
 *   - `items` prop is the canonical source of options
 *   - `<SelectContent>` accepts `<SelectItem>` children; we register them
 *     into a context, then render a RAC `<ListBox items={...}>` from them.
 */

interface PlexorSelectItemProps {
  value: string
  children?: React.ReactNode
  className?: string
}

interface PlexorSelectItemDescriptor {
  id: string
  label: React.ReactNode
  textValue?: string
  className?: string
}

const SelectItemContext = React.createContext<{
  items: PlexorSelectItemDescriptor[]
  register: (desc: PlexorSelectItemDescriptor) => void
} | null>(null)

interface PlexorItemShape {
  value: string
  label?: React.ReactNode
}

interface PlexorSelectRootProps {
  items?: PlexorItemShape[]
  value?: string
  defaultValue?: string
  onValueChange?: (value: string) => void
  isDisabled?: boolean
  disabled?: boolean
  placeholder?: string
  children?: React.ReactNode
  /**
   * Accessible name for the whole control. react-aria's `useLabel` warns
   * ("If you do not provide a visible label…") when the root gets none of
   * label/aria-label/aria-labelledby — forwarded here so callers can pass
   * them on `<Select>` like on any other field primitive.
   */
  "aria-label"?: string
  "aria-labelledby"?: string
  id?: string
}

function PlexorSelectRoot({ children, items, value, defaultValue, onValueChange, isDisabled, disabled, placeholder, "aria-label": ariaLabel, "aria-labelledby": ariaLabelledby, id }: PlexorSelectRootProps) {
  const [registeredItems, setRegisteredItems] = React.useState<PlexorSelectItemDescriptor[]>([])
  const register = React.useCallback((desc: PlexorSelectItemDescriptor) => {
    setRegisteredItems((prev) => {
      if (prev.some((p) => p.id === desc.id)) return prev
      return [...prev, desc]
    })
  }, [])

  const finalItems: PlexorSelectItemDescriptor[] = React.useMemo(() => {
    if (items && items.length > 0) {
      return items.map((it) => ({
        id: String(it.value),
        label: it.label,
        textValue: typeof it.label === "string" ? it.label : undefined,
      }))
    }
    return registeredItems
  }, [items, registeredItems])

  return (
    <SelectItemContext.Provider value={{ items: finalItems, register }}>
      <RACSelect
        data-slot="select"
        selectedKey={value}
        defaultSelectedKey={defaultValue}
        onSelectionChange={(key) => onValueChange?.(String(key))}
        isDisabled={isDisabled ?? disabled}
        placeholder={placeholder}
        aria-label={ariaLabel}
        aria-labelledby={ariaLabelledby}
        id={id}
      >
        {children}
      </RACSelect>
    </SelectItemContext.Provider>
  )
}

interface PlexorSelectValueProps extends Omit<React.ComponentProps<typeof RACSelectValue>, "children" | "placeholder"> {
  children?: React.ReactNode
  /** base-ui compat: also accepted; ignored because the placeholder is set on the parent `<Select>`. */
  placeholder?: string
}

function PlexorSelectValue({ className, placeholder: _placeholder, ...props }: PlexorSelectValueProps) {
  return (
    <RACSelectValue
      data-slot="select-value"
      className={cn("line-clamp-1 text-left", className)}
      {...props}
    />
  )
}

interface PlexorSelectTriggerProps extends Omit<React.ComponentProps<typeof PrimitiveButton>, "children"> {
  size?: "sm" | "default"
  isDisabled?: boolean
  disabled?: boolean
  children?: React.ReactNode
}

function PlexorSelectTrigger({
  className,
  size = "default",
  isDisabled,
  disabled,
  children,
  ...props
}: PlexorSelectTriggerProps) {
  return (
    <PrimitiveButton
      data-slot="select-trigger"
      data-size={size}
      isDisabled={isDisabled ?? disabled}
      className={cn(
        "flex h-7 w-full items-center justify-between gap-1.5 rounded-md border border-input bg-input/20 px-2 text-xs/relaxed whitespace-nowrap transition-colors outline-none",
        "focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/30",
        "disabled:cursor-not-allowed disabled:opacity-50",
        "aria-invalid:border-destructive aria-invalid:ring-2 aria-invalid:ring-destructive/20",
        "data-placeholder:text-muted-foreground",
        "dark:bg-input/30 dark:hover:bg-input/50 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40",
        "[&_svg]:pointer-events-none [&_svg]:shrink-0",
        className,
      )}
      {...props}
    >
      {children}
      <span className="pointer-events-none ml-1 flex shrink-0 items-center text-muted-foreground">
        <KeyboardArrowDown className="size-3.5" />
      </span>
    </PrimitiveButton>
  )
}

interface PlexorSelectContentProps extends React.HTMLAttributes<HTMLDivElement> {
  side?: "top" | "right" | "bottom" | "left"
  sideOffset?: number
  align?: "start" | "center" | "end"
  alignOffset?: number
  children?: React.ReactNode
  className?: string
}

function PlexorSelectContent({ children, className }: PlexorSelectContentProps) {
  const ctx = React.useContext(SelectItemContext)
  if (!ctx) return null
  return (
    <Popover
      data-slot="select-content"
      className={cn(
        "z-popover w-(--anchor-width) origin-(--transform-origin) bg-clip-padding",
        "overflow-hidden rounded-md bg-popover text-popover-foreground shadow-md ring-1 ring-foreground/10",
        "data-entering:animate-in data-entering:fade-in-0 data-entering:zoom-in-95",
        "data-exiting:animate-out data-exiting:fade-out-0 data-exiting:zoom-out-95",
        "duration-100",
        className,
      )}
    >
      <ListBox items={ctx.items} className="p-1">
        {(item) => (
          <ListBoxItem
            id={item.id}
            textValue={item.textValue}
            className={cn(
              "group/select-item relative flex cursor-default items-center gap-2 rounded-sm py-1.5 pr-2 pl-2 text-xs/relaxed outline-hidden select-none",
              "data-[focused=true]:bg-accent data-[focused=true]:text-accent-foreground",
              "data-[selected=true]:bg-accent/40 data-[selected=true]:text-accent-foreground",
              "data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
              item.className,
            )}
          >
            <span className="flex-1 whitespace-nowrap">{item.label}</span>
            {/* Check only visible on the selected item — otherwise every
               option looks ticked, which is what users see now. RAC sets
               data-selected=true on the ListBoxItem itself; the wrapper
               span inherits via group-data-[selected=true] from
               .group/select-item on the parent. */}
            <span
              aria-hidden
              className="ml-auto flex shrink-0 items-center justify-center opacity-0 group-data-[selected=true]/select-item:opacity-100"
            >
              <Check className="size-3.5 text-foreground group-data-[focused]/select-item:text-accent-foreground" />
            </span>
          </ListBoxItem>
        )}
      </ListBox>
      {children}
    </Popover>
  )
}

function PlexorSelectItem({ value, children, className }: PlexorSelectItemProps) {
  const ctx = React.useContext(SelectItemContext)
  const textValue = typeof children === "string" ? children : undefined
  React.useEffect(() => {
    ctx?.register({ id: String(value), label: children, textValue, className })
  }, [ctx, value, children, textValue, className])
  return null
}

function PlexorSelectGroup({ children }: { children?: React.ReactNode }) {
  void children
  return null
}

function PlexorSelectLabel({ children }: { children?: React.ReactNode }) {
  void children
  return null
}

type PlexorSelectSeparatorProps = React.HTMLAttributes<HTMLDivElement>

function PlexorSelectSeparator({ className, ...props }: PlexorSelectSeparatorProps) {
  return (
    <div
      data-slot="select-separator"
      className={cn("-mx-1 my-1 h-px bg-border/50", className)}
      {...props}
    />
  )
}

export {
  PlexorSelectRoot as Select,
  PlexorSelectValue as SelectValue,
  PlexorSelectTrigger as SelectTrigger,
  PlexorSelectContent as SelectContent,
  PlexorSelectItem as SelectItem,
  PlexorSelectGroup as SelectGroup,
  PlexorSelectLabel as SelectLabel,
  PlexorSelectSeparator as SelectSeparator,
}
