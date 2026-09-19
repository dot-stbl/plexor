import * as React from "react"

import { cn } from "@/lib/utils"

/**
 * Polymorphic rendering — the base-ui `useRender` + `mergeProps` pair,
 * rebuilt on plain React after the react-aria-components migration.
 *
 * Semantics preserved from base-ui:
 *   - no `render` prop → render the default intrinsic tag;
 *   - `render={<Element />}` → clone that element with our props merged in
 *     (className concatenated after the element's own, caller children win
 *     over the render element's children — same order as Button's
 *     composeRender);
 *   - `state` entries become `data-*` attributes; null/undefined/false
 *     entries are omitted (presence-based `data-x:` Tailwind styling).
 */

/** Polymorphic target: any element that accepts className + children. */
export type Renderable = React.ReactElement<{
  className?: string
  children?: React.ReactNode
}>

/** Props for a polymorphic primitive: intrinsic-tag props + `render`. */
export type PolymorphicProps<Tag extends keyof React.JSX.IntrinsicElements> =
  React.ComponentProps<Tag> & { render?: Renderable }

type PolymorphicState = Record<string, string | number | boolean | null | undefined>

function toDataAttributes(state: PolymorphicState): Record<string, unknown> {
  const attributes: Record<string, unknown> = {}
  for (const [key, value] of Object.entries(state)) {
    if (value !== null && value !== undefined && value !== false) {
      attributes[`data-${key}`] = value
    }
  }
  return attributes
}

export function polymorphic<Tag extends keyof React.JSX.IntrinsicElements>(
  defaultTagName: Tag,
  props: PolymorphicProps<Tag>,
  className: string,
  state?: PolymorphicState,
): React.ReactElement {
  const { render, ...rest } = props
  const dataAttributes = toDataAttributes(state ?? {})

  if (render !== undefined) {
    const original = render.props
    return React.cloneElement(render, {
      ...dataAttributes,
      ...rest,
      className: cn(original.className, className),
      children: rest.children ?? original.children,
    } as typeof original & Record<string, unknown>)
  }

  const Tag = defaultTagName as unknown as React.ElementType
  return (
    <Tag {...(dataAttributes as object)} {...(rest as object)} className={className} />
  )
}
