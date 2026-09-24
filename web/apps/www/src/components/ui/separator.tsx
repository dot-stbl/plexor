"use client"

import { Separator as SeparatorPrimitive } from "react-aria-components"

import { cn } from "@/lib/utils"

/**
 * Ported verbatim from `web/apps/console/src/shared/ui/primitives/separator.tsx`
 * (only the `@/lib/utils` import needed adjusting).
 */

export interface SeparatorProps extends React.ComponentProps<typeof SeparatorPrimitive> {
  orientation?: "horizontal" | "vertical"
}

function Separator({ className, orientation = "horizontal", ...props }: SeparatorProps) {
  return (
    <SeparatorPrimitive
      data-slot="separator"
      orientation={orientation}
      className={cn(
        "shrink-0 bg-border data-horizontal:h-px data-horizontal:w-full data-vertical:w-px data-vertical:self-stretch",
        className
      )}
      {...props}
    />
  )
}

export { Separator }
