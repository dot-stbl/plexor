"use client"

import * as React from "react"
import { ProgressBar as RACProgressBar } from "react-aria-components"

import { cn } from "@/lib/utils"

export interface ProgressProps extends React.ComponentProps<typeof RACProgressBar> {
  /** base-ui compat: alias for RAC's value. */
  value?: number
  /** base-ui compat: alias for RAC's maxValue. */
  max?: number
  className?: string
  children?: React.ReactNode
}

function Progress({ className, children, value, max, ...props }: ProgressProps) {
  return (
    <RACProgressBar
      value={value}
      maxValue={max}
      data-slot="progress"
      className={cn("flex flex-wrap gap-3", className)}
      {...props}
    >
      {children}
      <ProgressTrack>
        <ProgressIndicator />
      </ProgressTrack>
    </RACProgressBar>
  )
}

function ProgressTrack({ className, children, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      data-slot="progress-track"
      className={cn(
        "relative flex h-1 w-full items-center overflow-x-hidden rounded-md bg-muted",
        className
      )}
      {...props}
    >
      {children}
    </div>
  )
}

function ProgressIndicator({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      data-slot="progress-indicator"
      className={cn("h-full bg-primary transition-all", className)}
      {...props}
    />
  )
}

function ProgressLabel({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      data-slot="progress-label"
      className={cn("text-xs/relaxed font-medium", className)}
      {...props}
    />
  )
}

function ProgressValue({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      data-slot="progress-value"
      className={cn("ml-auto text-xs/relaxed text-muted-foreground tabular-nums", className)}
      {...props}
    />
  )
}

export {
  Progress,
  ProgressTrack,
  ProgressIndicator,
  ProgressLabel,
  ProgressValue,
}
