import { Button as ButtonPrimitive } from "@base-ui/react/button"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

/**
 * Plexor DS Button — sizes mapped to design-system heights:
 *   xs  → h-6  (24px, dense table actions)
 *   sm  → h-7  (28px, default row action)
 *   md  → h-8  (32px, bar component)        ← default
 *   lg  → h-10 (40px, hero CTA)
 *   xl  → h-12 (48px, page hero)
 *
 * Variants match .btn class in styles.css:
 *   primary / outline / secondary / ghost / destructive / link
 *
 * Flat button — no translate on active (Plexor DS is non-translating).
 * Focus uses `outline` (2px solid var(--accent), offset 2px) instead of
 * ring (Border) — matches `--shadow` policy where shadows are reserved
 * for floating UI only.
 */
const buttonVariants = cva(
  [
    // base
    "group/button inline-flex shrink-0 items-center justify-center gap-1.5 rounded-md border bg-clip-padding whitespace-nowrap font-medium transition-colors outline-none select-none",
    // focus-visible: Plexor DS outline, not ring
    "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring",
    // disabled
    "disabled:pointer-events-none disabled:opacity-50",
    // icon sizing inside
    "[&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-3.5",
  ],
  {
    variants: {
      variant: {
        // Plexor DS primary: bg=accent, text=accent-fg, border=accent
        default:
          "bg-primary text-primary-foreground border-primary hover:bg-[color-mix(in_oklch,var(--primary),var(--foreground)_5%)]",
        // Plexor DS btn: surface bg, border, ghost hover
        outline:
          "bg-card text-card-foreground border-border hover:bg-muted aria-expanded:bg-muted",
        secondary:
          "bg-secondary text-secondary-foreground border-transparent hover:bg-[color-mix(in_oklch,var(--secondary),var(--foreground)_5%)]",
        ghost:
          "border-transparent hover:bg-muted aria-expanded:bg-muted",
        // Plexor DS destructive — soft (text-only) and solid forms
        destructive:
          "bg-destructive/10 text-destructive border-destructive/20 hover:bg-destructive/20 focus-visible:outline-destructive/40 aria-expanded:bg-destructive/20",
        // Plexor DS danger: text-only, err-ink color, transparent border (sibling to destructive but milder)
        danger:
          "text-[var(--err-ink)] border-transparent hover:bg-[var(--err-soft)] hover:border-[var(--err)]",
        // Plexor DS danger-solid: filled red, white text, used for irreversible confirmations
        "danger-solid":
          "bg-[var(--err)] text-white border-[var(--err)] hover:bg-[oklch(52%_0.20_25)] hover:border-[oklch(52%_0.20_25)] focus-visible:outline-[oklch(45%_0.18_25)]",
        link: "border-transparent text-primary underline-offset-4 hover:underline",
      },
      size: {
        // xs: dense table button
        xs: "h-6 px-2 text-xs",
        // sm: row action button (Plexor DS default row-h=30 plus border = 28)
        sm: "h-7 px-2.5 text-xs",
        // md: bar/component button (Plexor DS 32)
        md: "h-8 px-3 text-xs",
        // default: alias for md (back-compat with shadcn-generated primitives)
        default: "h-8 gap-1 px-3 text-xs has-data-[icon=inline-end]:pr-2 has-data-[icon=inline-start]:pl-2 [&_svg:not([class*='size-'])]:size-3.5",
        // lg: hero CTA
        lg: "h-10 px-4 text-sm",
        // xl: page hero
        xl: "h-12 px-6 text-base",
        // icon-only variants
        icon: "size-8",
        "icon-xs": "size-6",
        "icon-sm": "size-7",
        "icon-lg": "size-10",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "md",
    },
  }
)

function Button({
  className,
  variant = "default",
  size = "md",
  ...props
}: ButtonPrimitive.Props & VariantProps<typeof buttonVariants>) {
  return (
    <ButtonPrimitive
      data-slot="button"
      className={cn(buttonVariants({ variant, size, className }))}
      {...props}
    />
  )
}

export { Button, buttonVariants }
