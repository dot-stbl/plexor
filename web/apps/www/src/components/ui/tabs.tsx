"use client"

import * as React from "react"
import {
  Tab as RACTab,
  TabList as RACTabList,
  TabPanel as RACTabPanel,
  Tabs as RACTabs,
} from "react-aria-components"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

/**
 * Ported verbatim from `web/apps/console/src/shared/ui/primitives/tabs.tsx`
 * (only the `@/lib/utils` import needed adjusting).
 *
 * Plexor Tabs — react-aria-components-backed.
 *
 * Compatibility shim: original base-ui API used `value` / `onValueChange`
 * on the root and `value` on TabsTrigger / TabsContent. RAC uses `selectedKey` /
 * `onSelectionChange` on the root and `id` on Tab / TabPanel.
 *
 * Note: RAC requires tabs to be defined via either the `items` collection API
 * or by wrapping Tab children in a Collection. To preserve the existing call
 * pattern of static <TabsTrigger> children, we forward the rendered children
 * through and pass `id` from the legacy `value` prop.
 */

interface PlexorTabsRootProps {
  /** base-ui compat: alias for RAC's selectedKey. */
  value?: string
  /** base-ui compat: alias for RAC's defaultSelectedKey. */
  defaultValue?: string
  /** base-ui compat: alias for RAC's onSelectionChange. */
  onValueChange?: (value: string) => void
  orientation?: "horizontal" | "vertical"
  className?: string
  children?: React.ReactNode
}

function Tabs({ className, orientation = "horizontal", value, defaultValue, onValueChange, children }: PlexorTabsRootProps) {
  return (
    <RACTabs
      data-slot="tabs"
      data-orientation={orientation}
      selectedKey={value}
      defaultSelectedKey={defaultValue}
      onSelectionChange={(key) => onValueChange?.(String(key))}
      orientation={orientation}
      className={cn(
        "group/tabs flex flex-col gap-2 data-vertical:flex-row",
        className
      )}
    >
      {children}
    </RACTabs>
  )
}

const tabsListVariants = cva(
  "group/tabs-list inline-flex w-fit items-center justify-center rounded-lg p-[3px] text-muted-foreground group-data-horizontal/tabs:h-8 group-data-vertical/tabs:h-fit group-data-vertical/tabs:flex-col data-[variant=line]:rounded-none",
  {
    variants: {
      variant: {
        default: "bg-muted",
        line: "gap-1 bg-transparent",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
)

interface PlexorTabsListProps extends VariantProps<typeof tabsListVariants> {
  className?: string
  children?: React.ReactNode
}

function TabsList({ className, variant = "default", children, ...props }: PlexorTabsListProps) {
  return (
    <RACTabList
      data-slot="tabs-list"
      data-variant={variant}
      className={cn(tabsListVariants({ variant }), className)}
      {...props}
    >
      {children}
    </RACTabList>
  )
}

interface PlexorTabsTriggerProps {
  /** base-ui compat: alias for RAC's id. */
  value?: string
  className?: string
  children?: React.ReactNode
  disabled?: boolean
}

function TabsTrigger({ className, value, disabled, children, ...props }: PlexorTabsTriggerProps) {
  return (
    <RACTab
      data-slot="tabs-trigger"
      id={value}
      isDisabled={disabled}
      className={cn(
        "relative inline-flex h-[calc(100%-1px)] flex-1 items-center justify-center gap-1.5 rounded-md border border-transparent px-1.5 py-0.5 text-xs font-medium whitespace-nowrap text-foreground/60 transition-all group-data-vertical/tabs:w-full group-data-vertical/tabs:justify-start group-data-vertical/tabs:py-[calc(--spacing(1.25))] hover:text-foreground focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-1 focus-visible:outline-ring disabled:pointer-events-none disabled:opacity-50 has-data-[icon=inline-end]:pr-1 has-data-[icon=inline-start]:pl-1 aria-disabled:pointer-events-none aria-disabled:opacity-50 dark:text-muted-foreground dark:hover:text-foreground [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-3.5",
        "group-data-[variant=line]/tabs-list:bg-transparent group-data-[variant=line]/tabs-list:data-selected:bg-transparent dark:group-data-[variant=line]/tabs-list:data-selected:border-transparent dark:group-data-[variant=line]/tabs-list:data-selected:bg-transparent",
        "data-selected:bg-background data-selected:text-foreground dark:data-selected:border-input dark:data-selected:bg-input/30 dark:data-selected:text-foreground",
        "after:absolute after:bg-foreground after:opacity-0 after:transition-opacity group-data-horizontal/tabs:after:inset-x-0 group-data-horizontal/tabs:after:bottom-[-5px] group-data-horizontal/tabs:after:h-0.5 group-data-vertical/tabs:after:inset-y-0 group-data-vertical/tabs:after:-right-1 group-data-vertical/tabs:after:w-0.5 group-data-[variant=line]/tabs-list:data-selected:after:opacity-100",
        className
      )}
      {...props}
    >
      {children}
    </RACTab>
  )
}

interface PlexorTabsContentProps {
  /** base-ui compat: alias for RAC's id. */
  value?: string
  className?: string
  children?: React.ReactNode
}

function TabsContent({ className, value, children, ...props }: PlexorTabsContentProps) {
  return (
    <RACTabPanel
      data-slot="tabs-content"
      id={value}
      className={cn("flex-1 text-xs/relaxed outline-none", className)}
      {...props}
    >
      {children}
    </RACTabPanel>
  )
}

export { Tabs, TabsList, TabsTrigger, TabsContent, tabsListVariants }
