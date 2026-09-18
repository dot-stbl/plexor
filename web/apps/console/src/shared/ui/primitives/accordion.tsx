import * as React from "react"
import {
  Disclosure as RACDisclosure,
  DisclosureGroup as RACDisclosureGroup,
  DisclosurePanel as RACDisclosurePanel,
  Heading as RACHeading,
  Button as RACButton,
} from "react-aria-components"
import { KeyboardArrowDown, KeyboardArrowUp } from "@nine-thirty-five/material-symbols-react/rounded/700"

import { cn } from "@/lib/utils"

/**
 * Plexor Accordion — react-aria-components-backed.
 *
 * base-ui's `<Accordion>` provides a Root + Item + Trigger + Panel.
 * RAC equivalent: `<DisclosureGroup>` + `<Disclosure>` + `<DisclosurePanel>`.
 * The Trigger is `<Button slot="trigger">` inside the Disclosure.
 */
interface PlexorAccordionRootProps {
  className?: string
  /** Open values (controlled). */
  value?: string[]
  /** Default open values (uncontrolled). */
  defaultValue?: string[]
  /** Open handler. */
  onValueChange?: (value: string[]) => void
  /** Multiple expand at once (default: true). */
  allowsMultipleExpanded?: boolean
  children?: React.ReactNode
}

function Accordion({ className, value, defaultValue, onValueChange, allowsMultipleExpanded, children }: PlexorAccordionRootProps) {
  return (
    <RACDisclosureGroup
      data-slot="accordion"
      className={cn(
        "flex w-full flex-col overflow-hidden rounded-md border",
        className
      )}
      expandedKeys={value ? new Set(value) : undefined}
      defaultExpandedKeys={defaultValue ? new Set(defaultValue) : undefined}
      onExpandedChange={(keys) => onValueChange?.(Array.from(keys).map(String))}
      allowsMultipleExpanded={allowsMultipleExpanded}
    >
      {children}
    </RACDisclosureGroup>
  )
}

interface PlexorAccordionItemProps extends React.HTMLAttributes<HTMLDivElement> {
  /** base-ui compat: maps to RAC's id. */
  value?: string
  /** base-ui compat: whether item starts open. */
  defaultOpen?: boolean
}

function AccordionItem({ className, value, defaultOpen, children, ...props }: PlexorAccordionItemProps) {
  return (
    <RACDisclosure
      data-slot="accordion-item"
      id={value}
      defaultExpanded={defaultOpen}
      className={cn("not-last:border-b data-expanded:bg-muted/50", className)}
      {...props}
    >
      {children}
    </RACDisclosure>
  )
}

interface PlexorAccordionTriggerProps extends React.HTMLAttributes<HTMLDivElement> {}

function AccordionTrigger({ className, children }: PlexorAccordionTriggerProps) {
  return (
    <RACHeading className="flex">
      <RACButton
        data-slot="accordion-trigger"
        slot="trigger"
        className={cn(
          "group/accordion-trigger relative flex flex-1 items-start justify-between gap-6 border border-transparent p-2 text-left text-xs/relaxed font-medium transition-all outline-none hover:underline aria-disabled:pointer-events-none aria-disabled:opacity-50 **:data-[slot=accordion-trigger-icon]:ml-auto **:data-[slot=accordion-trigger-icon]:size-4 **:data-[slot=accordion-trigger-icon]:text-muted-foreground",
          className
        )}
      >
        {children}
        <KeyboardArrowDown data-slot="accordion-trigger-icon" className="pointer-events-none shrink-0 group-aria-expanded/accordion-trigger:hidden" />
        <KeyboardArrowUp data-slot="accordion-trigger-icon" className="pointer-events-none hidden shrink-0 group-aria-expanded/accordion-trigger:inline" />
      </RACButton>
    </RACHeading>
  )
}

interface PlexorAccordionContentProps extends React.HTMLAttributes<HTMLDivElement> {}

function AccordionContent({ className, children }: PlexorAccordionContentProps) {
  return (
    <RACDisclosurePanel
      data-slot="accordion-content"
      className="overflow-hidden px-2 text-xs/relaxed data-entering:animate-accordion-down data-exiting:animate-accordion-up"
    >
      <div
        className={cn(
          "h-(--accordion-panel-height) pt-0 pb-4 [&_a]:underline [&_a]:underline-offset-3 [&_a]:hover:text-foreground [&_p:not(:last-child)]:mb-4",
          className
        )}
      >
        {children}
      </div>
    </RACDisclosurePanel>
  )
}

export { Accordion, AccordionItem, AccordionTrigger, AccordionContent }
