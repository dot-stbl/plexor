import * as React from "react"
import {
  Disclosure as RACDisclosure,
  DisclosurePanel as RACDisclosurePanel,
  Button as RACButton,
  Heading as RACHeading,
} from "react-aria-components"

/**
 * Ported verbatim from `web/apps/console/src/shared/ui/primitives/collapsible.tsx`
 * (no import-path changes needed — this file has no local imports).
 */

interface PlexorCollapsibleProps extends React.ComponentProps<typeof RACDisclosure> {
  /** base-ui compat: alias for isExpanded. */
  open?: boolean
  /** base-ui compat: alias for defaultExpanded. */
  defaultOpen?: boolean
  /** base-ui compat: alias for onExpandedChange. */
  onOpenChange?: (open: boolean) => void
}

function Collapsible(props: PlexorCollapsibleProps) {
  const { open, defaultOpen, onOpenChange, ...rest } = props
  return (
    <RACDisclosure
      data-slot="collapsible"
      isExpanded={open}
      defaultExpanded={defaultOpen}
      onExpandedChange={onOpenChange}
      {...rest}
    />
  )
}

interface PlexorCollapsibleTriggerProps extends Omit<React.ComponentProps<typeof RACButton>, "slot"> {
  asChild?: boolean
}

function CollapsibleTrigger({ asChild: _asChild, ...props }: PlexorCollapsibleTriggerProps) {
  return (
    <RACHeading>
      <RACButton data-slot="collapsible-trigger" slot="trigger" {...props} />
    </RACHeading>
  )
}

function CollapsibleContent({ ...props }: React.ComponentProps<typeof RACDisclosurePanel>) {
  return <RACDisclosurePanel data-slot="collapsible-content" {...props} />
}

export { Collapsible, CollapsibleTrigger, CollapsibleContent }
