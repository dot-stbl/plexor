'use client';

import type { ReactNode } from 'react';
import {
  Button,
  Disclosure,
  DisclosureGroup,
  DisclosurePanel,
  Heading,
} from 'react-aria-components';

/**
 * Accordions / Accordion — collapsible FAQ + troubleshooting recipes.
 *
 * Built on React Aria Components' `DisclosureGroup` + `Disclosure`
 * primitives so the keyboard model is correct out of the box (Tab
 * focuses each trigger, Enter / Space toggles, arrow keys walk the
 * group). The console app's `src/shared/ui/primitives/collapsible.tsx`
 * wraps the same primitive for in-app use — the docs MDX version
 * is a thin, docs-shaped wrapper that stays inside Plexor DS without
 * depending on the console package.
 *
 * Each `<Accordion>` renders a row (title + chevron) that expands
 * into a body. The chevron rotates 90° when the disclosure opens.
 * Default closed; pass `defaultExpanded` on a single `<Accordion>`
 * when the FAQ item needs to be open on first paint.
 */
export interface AccordionProps {
  readonly title: string;
  readonly defaultExpanded?: boolean;
  readonly children: ReactNode;
}

function Chevron({ expanded }: { readonly expanded: boolean }): ReactNode {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 16 16"
      width="14"
      height="14"
      className={`shrink-0 text-muted-foreground transition-transform duration-150 ease-out ${
        expanded ? 'rotate-90' : ''
      }`}
    >
      <path
        d="M6 4l4 4-4 4"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function Accordion({
  title,
  defaultExpanded = false,
  children,
}: AccordionProps): ReactNode {
  return (
    <Disclosure defaultExpanded={defaultExpanded} className="group/accordion border-b border-border last:border-b-0">
      {({ isExpanded }) => (
        <>
          <Heading className="m-0">
            <Button
              slot="trigger"
              className="flex w-full items-center justify-between gap-3 px-1 py-4 text-left text-base font-medium text-foreground outline-none transition-colors duration-150 hover:bg-muted/40 focus-visible:bg-muted/40"
            >
              <span>{title}</span>
              <Chevron expanded={isExpanded} />
            </Button>
          </Heading>
          <DisclosurePanel className="overflow-hidden bg-muted/30 px-1 pb-4 pt-1 text-md leading-6 text-muted-foreground [&_p]:my-2 [&_p:first-child]:mt-0 [&_p:last-child]:mb-0 [&_ul]:my-2 [&_ol]:my-2 [&_code]:font-mono [&_code]:text-[0.875em]">
            {children}
          </DisclosurePanel>
        </>
      )}
    </Disclosure>
  );
}

export interface AccordionsProps {
  readonly allowsMultiple?: boolean;
  readonly children: ReactNode;
}

/**
 * Accordions — wrapper for a stack of `<Accordion>` items.
 *
 * `allowsMultiple` controls whether more than one item can be
 * open at the same time. Default `true` because FAQ pages usually
 * want users to jump between questions without closing the one
 * they just opened; troubleshooting pages may set `false` to keep
 * focus narrow.
 */
export function Accordions({
  allowsMultiple = true,
  children,
}: AccordionsProps): ReactNode {
  return (
    <div className="docs-prose my-6 overflow-hidden rounded-lg border border-border bg-card">
      <DisclosureGroup allowsMultipleExpanded={allowsMultiple}>
        {children}
      </DisclosureGroup>
    </div>
  );
}
