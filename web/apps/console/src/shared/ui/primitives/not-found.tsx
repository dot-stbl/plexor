import type { ReactElement } from 'react';
import type { Icon } from '@nine-thirty-five/material-symbols-react';
import { WrongLocation } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useTranslation } from 'react-i18next';
import { Link, type NotFoundRouteProps } from '@tanstack/react-router';

import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { Button } from '@/shared/ui/primitives/button';

/**
 * NotFound — global 404 surface. Wired as the router's `notFoundComponent`
 * in `routes/__root.tsx`; renders when no route matches the current URL.
 *
 * Built on `EmptyState` for visual consistency with the rest of the console's
 * empty/missing-data surfaces. Title + description fall back to i18n keys
 * (`notFound.title` / `notFound.description`); a caller can override either
 * with a plain string for app-specific copy (e.g. an unauthenticated gate
 * that wants different wording). Home link defaults to `/` and the label to
 * `notFound.home`.
 *
 * Extends `NotFoundRouteProps` so TanStack Router can pass `isNotFound` /
 * `routeId` when rendering this as a `notFoundComponent`. We accept those
 * router props but only consume our own fields.
 *
 * @example
 *   // In routes/__root.tsx:
 *   export const Route = createRootRoute({
 *     component: RootComponent,
 *     notFoundComponent: NotFound,
 *   });
 */
export interface NotFoundProps extends Partial<NotFoundRouteProps> {
  /** Override the heading. Plain string beats i18n. */
  readonly title?: string;
  /** Override the body copy. Plain string beats i18n. */
  readonly description?: string;
  /** Where the home button links. Defaults to `/`. */
  readonly homeHref?: string;
  /** Override the home button label (no i18n). */
  readonly homeLabel?: string;
  /** Override the icon. Defaults to `WrongLocation`. */
  readonly icon?: Icon;
}

export function NotFound({
  title,
  description,
  homeHref = '/',
  homeLabel,
  icon: IconCmp = WrongLocation,
}: NotFoundProps = {}): ReactElement {
  const { t } = useTranslation();
  const heading = title ?? t('notFound.title');
  const body = description ?? t('notFound.description');
  const home = homeLabel ?? t('notFound.home');

  return (
    <div data-od-id="not-found" className="flex min-h-[60vh] items-center justify-center py-12">
      <EmptyState
        icon={IconCmp}
        title={heading}
        description={body}
        data-od-id="not-found-empty"
        action={
          <Button
            nativeButton={false}
            variant="default"
            render={<Link to={homeHref} />}
          >
            {home}
          </Button>
        }
      />
    </div>
  );
}