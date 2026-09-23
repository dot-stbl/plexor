import type { ReactNode } from 'react';
import { Link } from '@tanstack/react-router';
import type { Icon } from '@nine-thirty-five/material-symbols-react';
import {
  DiscFull,
  Package,
  Terminal,
} from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Choices / Choice — "pick between N options" cards for MDX pages.
 *
 * Used at chapter openers where the operator has to choose a path
 * through the docs (install method, deploy mode, hosting plan).
 * Each card is a real link — clicking it routes to a deeper docs
 * page via TanStack Router, not an abstract tab that swaps content
 * in place.
 *
 * Two components, one file:
 *   - `<Choices>` renders a responsive grid (1 col mobile, 2 on
 *     `sm:`, 3 on `md:`) of cards. Auto-collapses to fewer columns
 *     on narrow screens so the layout never overflows the article
 *     column.
 *   - `<Choice>` renders a single card: icon, h3 title, body.
 *     The whole card area is wrapped in a `<Link>` (or `<a>` if
 *     `href` is external) so the entire surface is the click
 *     target — the operator doesn't have to hunt for the title
 *     link.
 *
 * No hooks, no client state — the file is a server component.
 * Plexor DS tokens for the surface ( `border-border`, `bg-card`,
 * `text-foreground`, `text-muted-foreground` ); hover uses
 * `transition-colors` because Plexor DS uses colour transitions,
 * not shadows, for hover affordance.
 *
 * The icon registry is intentionally tiny: three names matching
 * the install seed (`disc`, `terminal`, `package`) plus a generic
 * fallback. Unknown icons render nothing rather than crashing the
 * page — the rest of the card stays useful. Adding a new icon
 * means adding one line to `ICON_REGISTRY`.
 */
export interface ChoicesProps {
  readonly children: ReactNode;
}

export function Choices({ children }: ChoicesProps): ReactNode {
  return (
    <div className="docs-prose my-8 not-prose grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3">
      {children}
    </div>
  );
}

const ICON_REGISTRY: Record<string, Icon> = {
  disc: DiscFull,
  terminal: Terminal,
  package: Package,
};

export interface ChoiceProps {
  readonly title: string;
  readonly icon?: string;
  readonly href: string;
  readonly children: ReactNode;
}

export function Choice({ title, icon, href, children }: ChoiceProps): ReactNode {
  const Icon = icon ? ICON_REGISTRY[icon] : undefined;
  const isExternal = href.startsWith('http://') || href.startsWith('https://');
  const cardClass =
    'group flex h-full flex-col gap-3 rounded-lg border border-border bg-card p-5 transition-colors duration-normal ease-out hover:border-foreground/40 hover:bg-accent/50';

  const body = (
    <>
      <div className="flex items-center gap-2 text-muted-foreground">
        {Icon ? (
          <Icon
            aria-hidden="true"
            className="h-6 w-6 shrink-0 transition-colors duration-normal ease-out group-hover:text-foreground"
          />
        ) : null}
        <h3 className="m-0 text-base font-semibold tracking-tight text-foreground">
          {title}
        </h3>
      </div>
      <div className="text-md leading-6 text-muted-foreground [&_p]:my-1 [&_p:first-child]:mt-0 [&_p:last-child]:mb-0 [&_ul]:my-1 [&_ol]:my-1 [&_a]:text-foreground [&_a]:underline [&_a]:decoration-border-2 [&_a]:underline-offset-4 hover:[&_a]:decoration-foreground">
        {children}
      </div>
    </>
  );

  if (isExternal) {
    return (
      <a href={href} className={cardClass} rel="noreferrer">
        {body}
      </a>
    );
  }

  return (
    <Link to={href} className={cardClass}>
      {body}
    </Link>
  );
}