import type { HeadMetaEntry } from './head-meta';

/**
 * Structural shape for the routesById entries this helper walks.
 * Typed loosely on purpose: TanStack Router's full `AnyRoute` type
 * is generic over context, loader-data schemas and a handful of other
 * things that vary per route. Fighting the router's exact generic
 * signature across two construction contexts (server entry + client
 * hook) for a 10-line walk loop buys nothing — the runtime behaviour
 * only needs `route.options.head?.()` to be callable.
 */
interface RouteHeadLike {
  readonly options?: {
    readonly head?: (
      args: {
        readonly matches: readonly unknown[];
        readonly match: unknown;
        readonly params: Record<string, unknown>;
        readonly loaderData?: unknown;
      },
    ) => { readonly meta?: readonly HeadMetaEntry[] } | undefined;
  };
}

interface RouterLike {
  readonly routesById: Readonly<Record<string, RouteHeadLike | undefined>>;
}

/** The single match descriptor this helper actually reads: `routeId`. */
interface MatchLike {
  readonly routeId: string;
}

/**
 * Walk the router's current `matches` from root to leaf and collect
 * each matched route's `head().meta` entries in walk order. Leaf
 * entries land last so `resolveHead()`'s natural last-wins loop lets
 * the leaf override ancestor defaults without the caller having to
 * dedupe by tag.
 */
export function collectRouteHead(
  router: RouterLike,
  matches: readonly MatchLike[],
): readonly HeadMetaEntry[] {
  const out: HeadMetaEntry[] = [];
  for (const match of matches) {
    const route = router.routesById[match.routeId];
    const head = route?.options?.head;
    if (!head) continue;
    const result = head({
      matches: matches as readonly unknown[],
      match,
      params: {},
      loaderData: undefined,
    });
    if (!result) continue;
    const meta = result.meta;
    if (!meta) continue;
    for (const entry of meta) out.push(entry);
  }
  return out;
}
