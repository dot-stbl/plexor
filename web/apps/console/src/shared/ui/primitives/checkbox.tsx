"use client"

import * as React from "react"
import { Checkbox as CheckboxPrimitive } from "react-aria-components"

import { cn } from "@/lib/utils"

export interface CheckboxProps
  extends Omit<
    React.ComponentProps<typeof CheckboxPrimitive>,
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
}

function Checkbox({
  className,
  checked,
  defaultChecked,
  onCheckedChange,
  disabled,
  ...props
}: CheckboxProps) {
  return (
    <CheckboxPrimitive
      data-slot="checkbox"
      className={cn(
        "peer relative flex size-4 shrink-0 items-center justify-center rounded-[4px] border border-input transition-all outline-none group-has-disabled/field:opacity-50 hover:border-foreground/40 after:absolute after:-inset-x-3 after:-inset-y-2 focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/30 disabled:cursor-not-allowed disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-2 aria-invalid:ring-destructive/20 aria-invalid:aria-checked:border-primary dark:bg-input/30 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40 data-checked:border-primary data-checked:bg-primary data-checked:text-primary-foreground dark:data-checked:bg-primary",
        className
      )}
      isSelected={checked}
      defaultSelected={defaultChecked}
      onChange={onCheckedChange}
      isDisabled={disabled}
      {...props}
    >
      <svg
        aria-hidden
        data-slot="checkbox-indicator"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth={3}
        className="grid size-3.5 place-content-center text-current transition-none group-data-[selected]:opacity-100 group-data-[selected]:scale-100 opacity-0 scale-75"
      >
        <polyline points="20 6 9 17 4 12" />
      </svg>
    </CheckboxPrimitive>
  )
}

export { Checkbox }
