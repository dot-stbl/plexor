"use client"

import * as React from "react"

import { cn } from "@/lib/utils"

/**
 * Plexor ScrollArea — react-aria-components does not ship a ScrollArea
 * primitive. We compose a styled viewport with native browser scrollbars,
 * matching the visual design of the previous base-ui version.
 */
type ScrollAreaProps = React.HTMLAttributes<HTMLDivElement> & {
  viewportClassName?: string
  scrollBarClassName?: string
}

const ScrollArea = React.forwardRef<HTMLDivElement, ScrollAreaProps>(function ScrollArea(
  { className, viewportClassName, scrollBarClassName, children, ...props },
  ref,
) {
  return (
    <div
      ref={ref}
      data-slot="scroll-area"
      className={cn("relative overflow-auto", className)}
      {...props}
    >
      <div
        data-slot="scroll-area-viewport"
        className={cn(
          "size-full rounded-[inherit] outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-1",
          viewportClassName
        )}
      >
        {children}
      </div>
      <style dangerouslySetInnerHTML={{ __html: scrollBarStyles(scrollBarClassName) }} />
    </div>
  )
})

const ScrollBar = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement> & { orientation?: "vertical" | "horizontal" }>(
  function ScrollBar({ className, orientation: _orientation = "vertical", ...props }, _ref) {
    // Custom scrollbars rely on the inline styles applied by `scrollBarStyles`;
    // this element is a no-op marker for backwards compatibility with consumers
    // that referenced <ScrollBar> for layout.
    return <div data-slot="scroll-area-scrollbar" data-orientation={_orientation} className={cn("hidden", className)} {...props} />
  },
)

function scrollBarStyles(className: string | undefined): string {
  const sel = ".plexor-scroll-area-host"
  return `
${sel} { scrollbar-color: oklch(var(--border) / 1) transparent; scrollbar-width: thin; }
${sel}::-webkit-scrollbar { width: 8px; height: 8px; }
${sel}::-webkit-scrollbar-track { background: transparent; }
${sel}::-webkit-scrollbar-thumb { background: oklch(var(--border) / 1); border-radius: 4px; }
${className ? ` ${sel} { ${className} }` : ""}
`
}

export { ScrollArea, ScrollBar }
