"use client"

import * as React from "react"

import { cn } from "@/lib/utils"

/**
 * Ported verbatim from `web/apps/console/src/shared/ui/primitives/scroll-area.tsx`
 * (only the `@/lib/utils` import needed adjusting — identical alias shape
 * in both apps).
 *
 * Plexor ScrollArea — react-aria-components does not ship a ScrollArea
 * primitive. We compose a styled viewport with native browser scrollbars,
 * matching the visual design of the previous base-ui version.
 *
 * `variant` controls the look of the native scrollbar:
 * - `default` (subtle) — a 6px rail that almost disappears until you hover.
 * - `themed` — a 10px rail with a visible thumb + hover state, themed via
 *   the Plexor tokens. Use for surfaces where the scrollbar is part of the
 *   visual identity (the launcher catalog). Off by default — most surfaces
 *   want the subtle one.
 */
type ScrollAreaProps = React.HTMLAttributes<HTMLDivElement> & {
  viewportClassName?: string
  scrollBarClassName?: string
  variant?: "default" | "themed"
}

const ScrollArea = React.forwardRef<HTMLDivElement, ScrollAreaProps>(function ScrollArea(
  { className, viewportClassName, scrollBarClassName, variant = "default", children, ...props },
  ref,
) {
  const hostClass = variant === "themed" ? "plexor-scroll-area-host plexor-scroll-area-themed" : "plexor-scroll-area-host"
  return (
    <div
      ref={ref}
      data-slot="scroll-area"
      data-bar-variant={variant}
      className={cn(hostClass, "relative overflow-auto", className)}
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
  const host = ".plexor-scroll-area-host"
  const themed = ".plexor-scroll-area-themed"
  return `
${host} { scrollbar-color: oklch(var(--border) / 1) transparent; scrollbar-width: thin; }
${host}::-webkit-scrollbar { width: 8px; height: 8px; }
${host}::-webkit-scrollbar-track { background: transparent; }
${host}::-webkit-scrollbar-thumb { background: oklch(var(--border) / 1); border-radius: 4px; }

/* Themed rail — wider, with a visible track, transparent until hovered.
   The thumb lifts in opacity + grows on hover so the rail reads as part of
   the surface rather than as a browser chrome artifact. */
${themed} { scrollbar-color: oklch(var(--muted-foreground) / 0.35) oklch(var(--muted) / 0.35); scrollbar-width: thin; }
${themed}::-webkit-scrollbar { width: 10px; height: 10px; }
${themed}::-webkit-scrollbar-track { background: oklch(var(--muted) / 0.35); border-radius: 999px; }
${themed}::-webkit-scrollbar-thumb { background: oklch(var(--muted-foreground) / 0.35); border-radius: 999px; border: 2px solid transparent; background-clip: padding-box; transition: background-color 150ms ease-out; }
${themed}::-webkit-scrollbar-thumb:hover { background: oklch(var(--muted-foreground) / 0.6); border: 2px solid transparent; background-clip: padding-box; }
${className ? ` ${host} { ${className} }` : ""}
`
}

export { ScrollArea, ScrollBar }
