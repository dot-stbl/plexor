import {
  Slider as SliderPrimitive,
  SliderFill,
  SliderThumb,
  SliderTrack,
} from "react-aria-components"

import { cn } from "@/lib/utils"

/**
 * Slider — react-aria-components-backed (base-ui compat).
 *
 * `min`/`max` map to `minValue`/`maxValue` and `onValueChange` maps to
 * `onChange` (same `number | number[]` payload). Structure: the base-ui
 * Control + Track merge into RAC's SliderTrack; Indicator → SliderFill;
 * one SliderThumb per value.
 */
interface SliderCompatProps
  extends Omit<
    React.ComponentProps<typeof SliderPrimitive>,
    "minValue" | "maxValue" | "onChange" | "defaultValue" | "value"
  > {
  min?: number
  max?: number
  value?: number | number[]
  defaultValue?: number | number[]
  onValueChange?: (value: number | number[]) => void
}

function Slider({
  className,
  defaultValue,
  value,
  min = 0,
  max = 100,
  onValueChange,
  ...props
}: SliderCompatProps) {
  // One thumb per value: array values render one thumb per entry; a
  // single number is a single-thumb slider (RAC requires the thumb count
  // to match the value count).
  const thumbCount = Array.isArray(value)
    ? value.length
    : Array.isArray(defaultValue)
      ? defaultValue.length
      : 1

  return (
    <SliderPrimitive
      className={cn("w-full", className)}
      data-slot="slider"
      defaultValue={defaultValue}
      value={value}
      minValue={min}
      maxValue={max}
      onChange={onValueChange}
      {...props}
    >
      <SliderTrack
        className="relative flex w-full touch-none items-center select-none data-disabled:opacity-50"
      >
        <span
          data-slot="slider-track"
          className="relative block h-2 w-full grow overflow-hidden rounded-full bg-foreground/30"
        >
          <SliderFill
            data-slot="slider-range"
            className="block h-full bg-primary"
          />
          {Array.from({ length: thumbCount }, (_, index) => (
            <SliderThumb
              data-slot="slider-thumb"
              key={index}
              className="block size-3 shrink-0 rounded-md border border-ring bg-white ring-ring/30 transition-[color,box-shadow] select-none after:absolute after:-inset-2 hover:ring-2 focus-visible:ring-2 focus-visible:outline-hidden active:ring-2 disabled:pointer-events-none disabled:opacity-50"
            />
          ))}
        </span>
      </SliderTrack>
    </SliderPrimitive>
  )
}

export { Slider }
