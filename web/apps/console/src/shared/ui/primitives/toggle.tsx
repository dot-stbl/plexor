"use client"

import * as React from "react"
import { ToggleButton as RACToggleButton } from "react-aria-components"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

const toggleVariants = cva(
  "group/toggle inline-flex items-center justify-center gap-1 rounded-md text-xs font-medium whitespace-nowrap transition-all outline-none hover:bg-muted hover:text-foreground focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:pointer-events-none disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-destructive/20 aria-pressed:bg-muted data-[selected]:bg-muted dark:aria-invalid:ring-destructive/40 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
  {
    variants: {
      variant: {
        default: "bg-transparent",
        outline: "border border-input bg-transparent hover:bg-muted",
      },
      size: {
        default:
          "h-7 min-w-7 px-2 has-data-[icon=inline-end]:pr-1.5 has-data-[icon=inline-start]:pl-1.5",
        sm: "h-6 min-w-6 rounded-[min(var(--radius-md),8px)] px-2 text-[0.625rem] has-data-[icon=inline-end]:pr-1.5 has-data-[icon=inline-start]:pl-1.5 [&_svg:not([class*='size-'])]:size-3",
        lg: "h-8 min-w-8 px-2.5 has-data-[icon=inline-end]:pr-1.5 has-data-[icon=inline-start]:pl-1.5",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)

export interface ToggleProps
  extends Omit<React.ComponentProps<typeof RACToggleButton>, "isSelected" | "defaultSelected" | "onChange">,
    VariantProps<typeof toggleVariants> {
  /** base-ui compat: alias for RAC's isSelected. */
  pressed?: boolean
  /** base-ui compat: alias for RAC's defaultSelected. */
  defaultPressed?: boolean
  /** base-ui compat: alias for RAC's onChange. */
  onPressedChange?: (pressed: boolean) => void
  className?: string
  children?: React.ReactNode
}

function Toggle({
  className,
  variant = "default",
  size = "default",
  pressed,
  defaultPressed,
  onPressedChange,
  ...props
}: ToggleProps) {
  return (
    <RACToggleButton
      data-slot="toggle"
      className={cn(toggleVariants({ variant, size, className }))}
      isSelected={pressed}
      defaultSelected={defaultPressed}
      onChange={onPressedChange}
      {...props}
    />
  )
}

export { Toggle, toggleVariants }
