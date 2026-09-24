import * as React from "react"
import { Button as ButtonPrimitive } from "react-aria-components"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

/**
 * Ported verbatim from `web/apps/console/src/shared/ui/primitives/button.tsx`
 * (react-aria-components-backed — despite the "base-ui compat" language
 * below, RAC is the actual runtime; the base-ui-shaped props are a
 * compatibility shim over it). Only the import path was adjusted.
 */

const buttonVariants = cva(
  "group/button inline-flex shrink-0 items-center justify-center rounded-md border border-transparent bg-clip-padding text-xs/relaxed font-medium whitespace-nowrap transition-all outline-none select-none focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/30 active:not-aria-[haspopup]:translate-y-px disabled:pointer-events-none disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-2 aria-invalid:ring-destructive/20 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
  {
    variants: {
      variant: {
        default: "bg-primary text-primary-foreground hover:bg-primary/80",
        outline:
          "border-border hover:bg-input/50 hover:text-foreground aria-expanded:bg-muted aria-expanded:text-foreground dark:bg-input/30",
        secondary:
          "bg-secondary text-secondary-foreground hover:bg-[color-mix(in_oklch,var(--secondary),var(--foreground)_5%)] aria-expanded:bg-secondary aria-expanded:text-secondary-foreground",
        ghost:
          "hover:bg-muted hover:text-foreground aria-expanded:bg-muted aria-expanded:text-foreground dark:hover:bg-muted/50",
        destructive:
          "bg-destructive/10 text-destructive hover:bg-destructive/20 focus-visible:border-destructive/40 focus-visible:ring-destructive/20 dark:bg-destructive/20 dark:hover:bg-destructive/30 dark:focus-visible:ring-destructive/40",
        link: "text-primary underline-offset-4 hover:underline",
      },
      size: {
        default:
          "h-7 gap-1 px-2 text-xs/relaxed has-data-[icon=inline-end]:pr-1.5 has-data-[icon=inline-start]:pl-1.5 [&_svg:not([class*='size-'])]:size-3.5",
        xs: "h-5 gap-1 rounded-sm px-2 text-[0.625rem] has-data-[icon=inline-end]:pr-1.5 has-data-[icon=inline-start]:pl-1.5 [&_svg:not([class*='size-'])]:size-2.5",
        sm: "h-6 gap-1 px-2 text-xs/relaxed has-data-[icon=inline-end]:pr-1.5 has-data-[icon=inline-start]:pl-1.5 [&_svg:not([class*='size-'])]:size-3",
        lg: "h-8 gap-1 px-2.5 text-xs/relaxed has-data-[icon=inline-end]:pr-2 has-data-[icon=inline-start]:pl-2 [&_svg:not([class*='size-'])]:size-4",
        icon: "size-7 [&_svg:not([class*='size-'])]:size-3.5",
        "icon-xs": "size-5 rounded-sm [&_svg:not([class*='size-'])]:size-2.5",
        "icon-sm": "size-6 [&_svg:not([class*='size-'])]:size-3",
        "icon-lg": "size-8 [&_svg:not([class*='size-'])]:size-4",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)

type BaseRenderable = React.ReactElement<{
  className?: string
  children?: React.ReactNode
}>

export interface ButtonProps
  extends Omit<
      React.ComponentProps<typeof ButtonPrimitive>,
      "onClick" | "onFocus" | "onBlur" | "onPointerDown" | "onPointerUp" | "onPointerEnter" | "onPointerLeave" | "isDisabled" | "className" | "children" | "render" | "value"
    >,
    VariantProps<typeof buttonVariants> {
  className?: string
  children?: React.ReactNode
  disabled?: boolean
  nativeButton?: boolean
  render?: BaseRenderable
  onClick?: React.MouseEventHandler<HTMLButtonElement>
  onFocus?: React.FocusEventHandler<HTMLButtonElement>
  onBlur?: React.FocusEventHandler<HTMLButtonElement>
  onPointerDown?: React.PointerEventHandler<HTMLButtonElement>
  onPointerUp?: React.PointerEventHandler<HTMLButtonElement>
  onPointerEnter?: React.PointerEventHandler<HTMLButtonElement>
  onPointerLeave?: React.PointerEventHandler<HTMLButtonElement>
  form?: string
  formAction?: string | ((formData: FormData) => void | Promise<void>)
  formEncType?: string
  formMethod?: string
  formNoValidate?: boolean
  formTarget?: string
  name?: string
  value?: string | number | readonly string[]
  type?: "button" | "submit" | "reset"
}

function composeRender(
  render: BaseRenderable,
  className: string,
  children?: React.ReactNode,
): BaseRenderable {
  const original = render.props
  // Button's children win over whatever the render element already had —
  // callers like `<Button render={<Link />}>{icon + label}</Button>` expect
  // their JSX to land inside the cloned element, not be silently dropped.
  // Falls back to the render element's own children when Button has none.
  const merged = {
    ...original,
    className: cn(original.className, className),
    children: children ?? original.children,
  } as typeof original
  return React.cloneElement(render, merged)
}

function Button({
  className,
  variant = "default",
  size = "default",
  disabled,
  nativeButton: _nativeButton,
  render,
  children,
  onClick,
  onFocus,
  onBlur,
  onPointerDown,
  onPointerUp,
  onPointerEnter,
  onPointerLeave,
  type = "button",
  value: _value,
  name: _name,
  form: _form,
  formAction: _formAction,
  formEncType: _formEncType,
  formMethod: _formMethod,
  formNoValidate: _formNoValidate,
  formTarget: _formTarget,
  ...props
}: ButtonProps) {
  if (render) {
    return composeRender(
      render,
      cn(buttonVariants({ variant, size }), className),
      children,
    )
  }

  // Cast through unknown: RAC's onClick event type uses FocusableElement,
  // but consumers (and existing call sites) use HTMLButtonElement signatures.
  // The two are structurally compatible at runtime.
  const racProps = {
    onClick: onClick as unknown as React.ComponentProps<typeof ButtonPrimitive>["onClick"],
    onFocus: onFocus as unknown as React.ComponentProps<typeof ButtonPrimitive>["onFocus"],
    onBlur: onBlur as unknown as React.ComponentProps<typeof ButtonPrimitive>["onBlur"],
    onPointerDown: onPointerDown as unknown as React.ComponentProps<typeof ButtonPrimitive>["onPointerDown"],
    onPointerUp: onPointerUp as unknown as React.ComponentProps<typeof ButtonPrimitive>["onPointerUp"],
    onPointerEnter: onPointerEnter as unknown as React.ComponentProps<typeof ButtonPrimitive>["onPointerEnter"],
    onPointerLeave: onPointerLeave as unknown as React.ComponentProps<typeof ButtonPrimitive>["onPointerLeave"],
  }

  return (
    <ButtonPrimitive
      data-slot="button"
      className={cn(buttonVariants({ variant, size }), className)}
      isDisabled={disabled}
      type={type}
      {...racProps}
      {...props}
    >
      {children}
    </ButtonPrimitive>
  )
}

export { Button, buttonVariants }
