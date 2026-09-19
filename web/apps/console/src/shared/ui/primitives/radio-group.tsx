import { RadioGroup as RadioGroupPrimitive, Radio as RadioPrimitive } from "react-aria-components"

import { cn } from "@/lib/utils"

/**
 * RadioGroup — react-aria-components-backed (base-ui compat).
 *
 * `value`/`onValueChange` map to RAC's `value`/`onChange`; `disabled`
 * maps to `isDisabled`. `data-checked:` classes became `data-selected:`
 * and `disabled:` became `data-disabled:` (RAC's DOM contract).
 */
interface RadioGroupCompatProps
  extends Omit<
    React.ComponentProps<typeof RadioGroupPrimitive>,
    "onChange" | "defaultValue" | "value"
  > {
  /** base-ui compat: controlled value. */
  value?: string
  /** base-ui compat: initial uncontrolled value. */
  defaultValue?: string
  /** base-ui compat: value change handler. */
  onValueChange?: (value: string) => void
}

function RadioGroup({
  value,
  defaultValue,
  onValueChange,
  ...props
}: RadioGroupCompatProps) {
  return (
    <RadioGroupPrimitive
      data-slot="radio-group"
      value={value}
      defaultValue={defaultValue}
      onChange={onValueChange}
      {...props}
    />
  )
}

interface RadioGroupItemCompatProps
  extends Omit<React.ComponentProps<typeof RadioPrimitive>, "isDisabled"> {
  disabled?: boolean
}

function RadioGroupItem({ className, disabled, ...props }: RadioGroupItemCompatProps) {
  return (
    <RadioPrimitive
      data-slot="radio-group-item"
      isDisabled={disabled}
      className={cn(
        "group/radio-group-item peer relative flex aspect-square size-4 shrink-0 rounded-full border border-input outline-none after:absolute after:-inset-x-3 after:-inset-y-2 focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 data-disabled:cursor-not-allowed data-disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20 aria-invalid:aria-checked:border-primary dark:bg-input/30 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40 data-selected:border-primary data-selected:bg-primary data-selected:text-primary-foreground dark:data-selected:bg-primary",
        className
      )}
      {...props}
    >
      {({ isSelected }) => (
        <span
          data-slot="radio-group-indicator"
          className="flex size-4 items-center justify-center"
        >
          {isSelected && (
            <span className="absolute top-1/2 left-1/2 size-2 -translate-x-1/2 -translate-y-1/2 rounded-full bg-primary-foreground" />
          )}
        </span>
      )}
    </RadioPrimitive>
  )
}

export { RadioGroup, RadioGroupItem }
