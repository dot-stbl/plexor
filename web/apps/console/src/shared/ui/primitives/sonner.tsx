"use client"

import { useTheme } from "next-themes"
import { Toaster as Sonner, type ToasterProps } from "sonner"
import {
  Cancel,
  CheckCircle,
  Info,
  ProgressActivity,
  Warning
} from '@nine-thirty-five/material-symbols-react/rounded/700';const Toaster = ({ ...props }: ToasterProps) => {
  const { theme = "system" } = useTheme()

  return (
    <Sonner
      theme={theme as ToasterProps["theme"]}
      className="toaster group"
      icons={{
        success: (
          <CheckCircle strokeWidth={2} className="size-4"  />
        ),
        info: (
          <Info strokeWidth={2} className="size-4"  />
        ),
        warning: (
          <Warning strokeWidth={2} className="size-4"  />
        ),
        error: (
          <Cancel strokeWidth={2} className="size-4"  />
        ),
        loading: (
          <ProgressActivity strokeWidth={2} className="size-4 animate-spin"  />
        ),
      }}
      style={
        {
          "--normal-bg": "var(--popover)",
          "--normal-text": "var(--popover-foreground)",
          "--normal-border": "var(--border)",
          "--border-radius": "var(--radius)",
        } as React.CSSProperties
      }
      toastOptions={{
        classNames: {
          toast: "cn-toast",
        },
      }}
      {...props}
    />
  )
}

export { Toaster }
