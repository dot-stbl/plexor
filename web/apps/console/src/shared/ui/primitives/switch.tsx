"use client"

import * as React from "react"
import { Switch as SwitchPrimitive } from "react-aria-components"

import { cn } from "@/lib/utils"

export interface SwitchProps
  extends Omit<
    React.ComponentProps<typeof SwitchPrimitive>,
    "isSelected" | "defaultSelected" | "onChange" | "isDisabled"
  > {
  /** base-ui alias for isSelected. */
  checked?: boolean
  /** base-ui alias for defaultSelected. */
  defaultChecked?: boolean
  /** base-ui alias for onChange. */
  onCheckedChange?: (checked: boolean) => void
  /** HTML disabled attribute. */
  disabled?: boolean
  size?: "sm" | "default"
}

function Switch({
  className,
  size = "default",
  checked,
  defaultChecked,
  onCheckedChange,
  disabled,
  ...props
}: SwitchProps) {
  return (
    <SwitchPrimitive
      data-slot="switch"
      data-size={size}
      className={cn(
        "peer group/switch relative inline-flex shrink-0 items-center rounded-full border border-transparent transition-all outline-none bg-input after:absolute after:-inset-x-3 after:-inset-y-2 focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/30 aria-invalid:border-destructive aria-invalid:ring-2 aria-invalid:ring-destructive/20 data-[size=default]:h-[16.6px] data-[size=default]:w-[28px] data-[size=sm]:h-[14px] data-[size=sm]:w-[24px] dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40 data-selected:bg-primary dark:bg-input/80 dark:data-selected:bg-primary data-disabled:cursor-not-allowed data-disabled:opacity-50",
        className
      )}
      isSelected={checked}
      defaultSelected={defaultChecked}
      onChange={onCheckedChange}
      isDisabled={disabled}
      {...props}
    >
      <span
        data-slot="switch-thumb"
        className="pointer-events-none block rounded-full bg-background ring-0 transition-transform group-data-[size=default]/switch:size-3.5 group-data-[size=sm]/switch:size-3 group-data-[size=default]/switch:group-data-[selected]/switch:translate-x-[calc(100%-2px)] group-data-[size=sm]/switch:group-data-[selected]/switch:translate-x-[calc(100%-2px)] dark:group-data-[selected]/switch:bg-primary-foreground"
      />
    </SwitchPrimitive>
  )
}

export { Switch }
