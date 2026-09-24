import type { ReactNode } from 'react';

/**
 * PlatformMatrix — compact styled table for "where does X run" pages.
 *
 * Renders a CSS-grid table where each row pairs a Plexor capability
 * (e.g. "Compute", "Networking") with the deployment tier on which
 * the capability ships. Tier 0 = control plane (always on),
 * Tier 1 = single host, Tier 2+ = multi-host / future phases.
 *
 * Two props:
 * - `rows`: a readonly list of `[capability, tierCell]` tuples. Caller
 *   types the cells; the component only handles layout.
 * - `caption`: optional `<figcaption>` for screen-reader context.
 */
export interface PlatformMatrixRow {
  readonly capability: string;
  readonly tier: string;
  readonly notes?: string;
}

export interface PlatformMatrixProps {
  readonly rows: readonly PlatformMatrixRow[];
  readonly caption?: string;
}

export function PlatformMatrix({ rows, caption }: PlatformMatrixProps): ReactNode {
  return (
    <figure className="my-6 overflow-x-auto">
      <table className="docs-prose-table w-full border-collapse text-sm">
        <thead>
          <tr className="border-b border-border-2 text-left">
            <th className="py-2 pr-4 font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
              Capability
            </th>
            <th className="py-2 pr-4 font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
              Tier
            </th>
            <th className="py-2 font-mono text-[10px] font-medium uppercase tracking-[0.14em] text-muted-2">
              Notes
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.capability} className="border-b border-border">
              <td className="py-2 pr-4 align-top text-foreground">{row.capability}</td>
              <td className="py-2 pr-4 align-top text-muted-foreground">{row.tier}</td>
              <td className="py-2 align-top text-muted-2">{row.notes ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {caption ? (
        <figcaption className="mt-2 text-center text-xs text-muted-2">
          {caption}
        </figcaption>
      ) : null}
    </figure>
  );
}
