import * as React from "react"

import { cn } from "@/lib/utils"

/**
 * Card primitive — Plexor console content surface.
 *
 * ## Padding convention
 *
 * The card and its children drive their internal padding from a single CSS
 * variable, `--card-spacing`. `Card` sets the variable on the root
 * (`py-(--card-spacing)`); `CardHeader`, `CardContent`, and `CardFooter`
 * reference it for their horizontal / vertical padding. Vertical rhythm
 * between sections is padding-driven (CardContent's `pt`), NOT gap-driven —
 * cards that zero the root (`className="gap-0 p-0"`) for edge-to-edge
 * headers still get correct header→content separation.
 *
 * | `size` prop  | `--card-spacing` | Resolved padding |
 * |--------------|------------------|------------------|
 * | `"default"`  | `--spacing(4)`   | 1rem (16px)      |
 * | `"sm"`       | `--spacing(3)`   | 0.75rem (12px)   |
 *
 * Rules of thumb:
 *
 * - **Default content cards** — leave `CardContent` alone. The 1rem padding
 *   is already applied via the CSS variable. Avoid `p-*` on `CardContent` —
 *   it stacks on top of the variable and produces double padding (the bug
 *   that motivated this convention).
 * - **Edge-to-edge children** — set `CardContent className="p-0"` and put
 *   padding on the inner element (e.g. `<div className="p-6">` for an
 *   empty / loading / error state). This pattern keeps Tables flush with
 *   the card border while still giving the empty state breathing room.
 * - **Tighter cards** — use `<Card size="sm">` for compact forms (login,
 *   short dialogs). Use the `size` prop, do not add `p-3` / `p-4` overrides.
 * - **Spacious cards** — pass a single utility to override the variable
 *   directly (`<Card className="[--card-spacing:--spacing(6)]">`). Reach for
 *   this on hero / marketing surfaces, not on admin forms.
 *
 * Do not mix the `p-*` override approach with the `--card-spacing` variable
 * on the same element. Pick one: override the variable, or set `p-0` and
 * pad children explicitly.
 */
function Card({
  className,
  size = "default",
  ...props
}: React.ComponentProps<"div"> & { size?: "default" | "sm" }) {
  return (
    <div
      data-slot="card"
      data-size={size}
      className={cn(
        "group/card flex flex-col overflow-hidden rounded-lg bg-card py-(--card-spacing) text-xs/relaxed text-card-foreground ring-1 ring-foreground/10 [--card-spacing:--spacing(4)] has-[>img:first-child]:pt-0 data-[size=sm]:[--card-spacing:--spacing(3)] *:[img:first-child]:rounded-t-lg *:[img:last-child]:rounded-b-lg",
        className
      )}
      {...props}
    />
  )
}

function CardHeader({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="card-header"
      className={cn(
        "group/card-header @container/card-header grid auto-rows-min items-start gap-1 rounded-t-lg px-(--card-spacing) has-data-[slot=card-action]:grid-cols-[1fr_auto] has-data-[slot=card-description]:grid-rows-[auto_auto] [.border-b]:pb-(--card-spacing)",
        className
      )}
      {...props}
    />
  )
}

function CardTitle({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="card-title"
      className={cn("font-heading text-sm font-medium", className)}
      {...props}
    />
  )
}

function CardDescription({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="card-description"
      className={cn("text-xs/relaxed text-muted-foreground", className)}
      {...props}
    />
  )
}

function CardAction({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="card-action"
      className={cn(
        "col-start-2 row-span-2 row-start-1 self-start justify-self-end",
        className
      )}
      {...props}
    />
  )
}

/**
 * CardContent — the body slot of a Card.
 *
 * Padding is provided by the `--card-spacing` CSS variable set on the
 * enclosing `Card` (see the Card JSDoc for the convention). `pt` comes from
 * the variable so the body never sits flush against a bordered CardHeader
 * above it; horizontal padding via `px`. Do not add `p-*` classes here
 * unless you are intentionally stacking on top of the variable — for
 * edge-to-edge children, prefer `p-0` on `CardContent` (tailwind-merge
 * resolves `pt-(--card-spacing)` vs `p-0` correctly) with padding on the
 * inner element instead.
 */
function CardContent({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="card-content"
      className={cn("px-(--card-spacing) pt-(--card-spacing)", className)}
      {...props}
    />
  )
}

function CardFooter({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="card-footer"
      className={cn(
        "flex items-center rounded-b-lg px-(--card-spacing) [.border-t]:pt-(--card-spacing)",
        className
      )}
      {...props}
    />
  )
}

export {
  Card,
  CardHeader,
  CardFooter,
  CardTitle,
  CardAction,
  CardDescription,
  CardContent,
}
