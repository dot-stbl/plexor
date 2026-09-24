"use client"

import * as React from "react"
import { Pressable } from "react-aria-components"

import { cn } from "@/lib/utils"

/**
 * DropdownMenuTrigger — see `dropdown-menu.tsx` (barrel) for the
 * compatibility-shim overview shared by the whole primitive.
 */
interface DropdownMenuTriggerProps {
  render?: React.ReactNode
  className?: string
  children?: React.ReactNode
  isDisabled?: boolean
  disabled?: boolean
  onPress?: () => void
}

export function DropdownMenuTrigger({ render, className, children, isDisabled, disabled, onPress }: DropdownMenuTriggerProps) {
  const target = (render ?? children) as React.ReactElement | undefined
  const disabledState = isDisabled ?? disabled
  if (!React.isValidElement(target)) {
    return (
      <Pressable isDisabled={disabledState}>
        <button
          type="button"
          className={className}
          disabled={disabledState}
          onClick={onPress}
          data-slot="dropdown-menu-trigger"
        >
          {children}
        </button>
      </Pressable>
    )
  }
  // NB: no <Pressable> wrapper here. `MenuTrigger` (react-aria-components)
  // wires open/close via an internal `PressResponder` context, which our
  // own RAC-backed `Button` already consumes through its own `usePress()`
  // call — the same context a `<Pressable>` wrapper would read. Wrapping an
  // already-RAC-aware `Button` in `<Pressable>` on top of that is redundant,
  // and it actively causes a false-positive react-aria dev warning
  // ("<Pressable> child must be focusable") whenever the trigger is
  // conditionally hidden via a responsive class (e.g. `md:hidden`) at the
  // viewport width present at mount — `isFocusable()` checks the *rendered*
  // DOM node's visibility, which a CSS breakpoint can make false on an
  // otherwise perfectly focusable button. `<Pressable>` stays reserved for
  // the fallback branch above, which clones a bare, non-RAC `<button>`.
  const targetProps = target.props as { className?: string }
  const mergedClassName = cn(targetProps.className, className)
  // Only forward `children` into the cloned target when the caller passed a
  // `render` element. Without `render`, `target` IS `children` (a single
  // element the caller wants rendered as-is), and re-passing it would
  // self-nest the element. With `render`, the render element is the target
  // and the caller's siblings are the children we need to thread through;
  // Button.composeRender's `children ?? original.children` then receives them.
  const clonedProps = {
    className: mergedClassName,
    ...(render !== undefined ? { children } : {}),
    ...(disabledState !== undefined ? { isDisabled: disabledState } : {}),
  }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  return React.cloneElement(target, clonedProps as any)
}
DropdownMenuTrigger.displayName = "PlexorDropdownMenuTrigger"
