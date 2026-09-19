"use client"

import * as React from "react"
import {
  ToggleButton,
  ToggleButtonGroup as ToggleButtonGroupPrimitive,
} from "react-aria-components"
import { type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"
import { toggleVariants } from "@/shared/ui/primitives/toggle"

/**
 * ToggleGroup — react-aria-components-backed (base-ui compat).
 *
 * Base UI's ToggleGroup holds an ARRAY of active values; RAC's
 * ToggleButtonGroup with selectionMode="multiple" matches. `value`/
 * `onValueChange` map to `selectedKeys`/`onSelectionChange`; item
 * identity is the ToggleButton `id` (fed from the base-ui `value` prop).
 * `data-pressed:` classes became `data-selected:` (RAC DOM contract).
 */
const ToggleGroupContext = React.createContext<
  VariantProps<typeof toggleVariants> & {
    spacing?: number
    orientation?: "horizontal" | "vertical"
  }
>({
  size: "default",
  variant: "default",
  spacing: 2,
  orientation: "horizontal",
})

interface ToggleGroupCompatProps
  extends Omit<
    React.ComponentProps<typeof ToggleButtonGroupPrimitive>,
    "selectedKeys" | "defaultSelectedKeys" | "onSelectionChange" | "selectionMode"
  > {
  value?: string[]
  defaultValue?: string[]
  onValueChange?: (value: string[]) => void
  variant?: VariantProps<typeof toggleVariants>["variant"]
  size?: VariantProps<typeof toggleVariants>["size"]
  spacing?: number
  orientation?: "horizontal" | "vertical"
}

function ToggleGroup({
  className,
  variant,
  size,
  spacing = 2,
  orientation = "horizontal",
  value,
  defaultValue,
  onValueChange,
  children,
  ...props
}: ToggleGroupCompatProps) {
  return (
    <ToggleButtonGroupPrimitive
      data-slot="toggle-group"
      data-variant={variant}
      data-size={size}
      data-spacing={spacing}
      data-orientation={orientation}
      selectionMode="multiple"
      selectedKeys={value}
      defaultSelectedKeys={defaultValue}
      onSelectionChange={
        onValueChange !== undefined
          ? (keys) => onValueChange([...keys].map(String))
          : undefined
      }
      style={{ "--gap": spacing } as React.CSSProperties}
      className={cn(
        "group/toggle-group flex w-fit flex-row items-center gap-[--spacing(var(--gap))] rounded-md data-[size=sm]:rounded-[min(var(--radius-md),8px)] data-vertical:flex-col data-vertical:items-stretch",
        className
      )}
      {...props}
    >
      <ToggleGroupContext.Provider
        value={{ variant, size, spacing, orientation }}
      >
        {children as React.ReactNode}
      </ToggleGroupContext.Provider>
    </ToggleButtonGroupPrimitive>
  )
}

interface ToggleGroupItemCompatProps {
  /** base-ui compat: identifies the item in the group's value array. */
  value: string
  children?: React.ReactNode
  className?: string
  variant?: VariantProps<typeof toggleVariants>["variant"]
  size?: VariantProps<typeof toggleVariants>["size"]
  disabled?: boolean
}

function ToggleGroupItem({
  className,
  children,
  variant = "default",
  size = "default",
  value,
  disabled,
  ...props
}: ToggleGroupItemCompatProps) {
  const context = React.useContext(ToggleGroupContext)

  return (
    <ToggleButton
      id={value}
      isDisabled={disabled}
      data-slot="toggle-group-item"
      data-variant={context.variant || variant}
      data-size={context.size || size}
      data-spacing={context.spacing}
      className={cn(
        "shrink-0 group-data-[spacing=0]/toggle-group:rounded-none group-data-[spacing=0]/toggle-group:px-2 focus:z-10 focus-visible:z-10 group-data-[spacing=0]/toggle-group:has-data-[icon=inline-end]:pr-1.5 group-data-[spacing=0]/toggle-group:has-data-[icon=inline-start]:pl-1.5 group-data-horizontal/toggle-group:data-[spacing=0]:first:rounded-l-md group-data-vertical/toggle-group:data-[spacing=0]:first:rounded-t-md group-data-horizontal/toggle-group:data-[spacing=0]:last:rounded-r-md group-data-vertical/toggle-group:data-[spacing=0]:last:rounded-b-md group-data-horizontal/toggle-group:data-[spacing=0]:data-[variant=outline]:border-l-0 group-data-vertical/toggle-group:data-[spacing=0]:data-[variant=outline]:border-t-0 group-data-horizontal/toggle-group:data-[spacing=0]:data-[variant=outline]:first:border-l group-data-vertical/toggle-group:data-[spacing=0]:data-[variant=outline]:first:border-t",
        toggleVariants({
          variant: context.variant || variant,
          size: context.size || size,
        }),
        className
      )}
      {...props}
    >
      {children}
    </ToggleButton>
  )
}

export { ToggleGroup, ToggleGroupItem }
