import * as React from "react"

import { cn } from "@/lib/utils"

/**
 * Avatar primitives — pure React (no library dependency).
 *
 * The React Aria migration explicitly excluded Avatar (no RAC equivalent
 * for the `name → initials + optional image` pattern, and the only
 * consumer was a single sidebar footer initials chip). To avoid the risk
 * of a future base-ui v1 Avatar rendering quirk silently breaking the
 * sidebar, we ship a tiny pure-React implementation:
 *
 *   <Avatar name="Jane Doe" />                          → renders "JD"
 *   <Avatar name="Jane Doe" src="/jane.jpg" />          → renders <img>
 *   <Avatar><AvatarFallback>JD</AvatarFallback></Avatar>  → renders custom fallback
 *
 * Group / badge variants exist for parity with the prior API but have
 * no production consumer today.
 */

type AvatarSize = "default" | "sm" | "lg"

/** Derive two-letter initials from a display name. */
function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return ""
  const first = parts[0]?.[0] ?? ""
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? "") : ""
  return (first + last).toUpperCase()
}

export interface AvatarProps extends React.ComponentProps<"div"> {
  size?: AvatarSize
  /** Display name; when no `src`/`children` are given, the initials are derived from this. */
  name?: string
  /** Image URL. When present, the avatar renders an `<img>` instead of initials. */
  src?: string
}

function Avatar({
  className,
  size = "default",
  name,
  src,
  children,
  ...props
}: AvatarProps) {
  const hasImage = typeof src === "string" && src.length > 0
  const hasCustomChildren = React.Children.count(children) > 0

  return (
    <div
      data-slot="avatar"
      data-size={size}
      className={cn(
        "group/avatar relative flex size-8 shrink-0 rounded-lg select-none overflow-hidden after:absolute after:inset-0 after:rounded-lg after:border after:border-border after:mix-blend-darken data-[size=lg]:size-10 data-[size=sm]:size-6 dark:after:mix-blend-lighten",
        className,
      )}
      {...props}
    >
      {hasImage ? (
        <AvatarImage src={src} alt={name ?? ""} />
      ) : hasCustomChildren ? (
        children
      ) : (
        <AvatarFallback>{getInitials(name ?? "")}</AvatarFallback>
      )}
    </div>
  )
}

export interface AvatarImageProps extends Omit<React.ComponentProps<"img">, "src"> {
  src?: string
  alt?: string
}

function AvatarImage({ className, src, alt, ...props }: AvatarImageProps) {
  return (
    <img
      data-slot="avatar-image"
      src={src}
      alt={alt ?? ""}
      className={cn("aspect-square size-full rounded-lg object-cover", className)}
      {...props}
    />
  )
}

export type AvatarFallbackProps = React.ComponentProps<"div">

function AvatarFallback({ className, ...props }: AvatarFallbackProps) {
  return (
    <div
      data-slot="avatar-fallback"
      className={cn(
        "flex size-full items-center justify-center rounded-lg bg-muted text-sm text-muted-foreground group-data-[size=sm]/avatar:text-xs",
        className,
      )}
      {...props}
    />
  )
}

function AvatarBadge({ className, ...props }: React.ComponentProps<"span">) {
  return (
    <span
      data-slot="avatar-badge"
      className={cn(
        "absolute right-0 bottom-0 z-popover inline-flex items-center justify-center rounded-md bg-primary text-primary-foreground bg-blend-color ring-2 ring-background select-none",
        "group-data-[size=sm]/avatar:size-2 group-data-[size=sm]/avatar:[&>svg]:hidden",
        "group-data-[size=default]/avatar:size-2.5 group-data-[size=default]/avatar:[&>svg]:size-2",
        "group-data-[size=lg]/avatar:size-3 group-data-[size=lg]/avatar:[&>svg]:size-2",
        className,
      )}
      {...props}
    />
  )
}

function AvatarGroup({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="avatar-group"
      className={cn(
        "group/avatar-group flex -space-x-2 *:data-[slot=avatar]:ring-2 *:data-[slot=avatar]:ring-background *:data-[slot=avatar]:rounded-lg",
        className,
      )}
      {...props}
    />
  )
}

function AvatarGroupCount({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="avatar-group-count"
      className={cn(
        "relative flex size-8 shrink-0 items-center justify-center rounded-lg bg-muted text-xs/relaxed text-muted-foreground ring-2 ring-background group-has-data-[size=lg]/avatar-group:size-10 group-has-data-[size=sm]/avatar-group:size-6 [&>svg]:size-4 group-has-data-[size=lg]/avatar-group:[&>svg]:size-5 group-has-data-[size=sm]/avatar-group:[&>svg]:size-3",
        className,
      )}
      {...props}
    />
  )
}

export {
  Avatar,
  AvatarImage,
  AvatarFallback,
  AvatarGroup,
  AvatarGroupCount,
  AvatarBadge,
}